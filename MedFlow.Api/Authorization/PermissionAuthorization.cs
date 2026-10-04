using System.Security.Claims;
using MedFlow.Core;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Authorization;

/// <summary>
/// Requires the signed-in staff member's clinic role to hold the permission (see <see cref="PermissionMatrix"/>).
/// This is the only way a staff endpoint is authorized; the check happens in <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";
    public Permission Permission { get; }

    public HasPermissionAttribute(Permission permission)
    {
        Permission = permission;
        Policy = PolicyPrefix + permission;
    }
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public Permission Permission { get; }
    public PermissionRequirement(Permission permission) => Permission = permission;
}

/// <summary>
/// Central staff authorization. Resolves the caller's clinic scope from the database on every request (so a
/// deactivation or role change applies immediately), denies patient tokens, users without an active membership and
/// roles lacking the permission, and exposes the scope to the action through <see cref="ClinicScopeExtensions"/>.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IClinicService _clinics;
    public PermissionAuthorizationHandler(IClinicService clinics) => _clinics = clinics;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        // Portal tokens never satisfy a staff permission
        if (context.User.IsInRole(Roles.Patient)) return;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return;

        var http = context.Resource as HttpContext;
        var scope = http?.Items[ClinicScopeExtensions.ItemKey] as ClinicScope ?? await _clinics.ResolveScopeAsync(userId);
        if (scope == null || scope.UserId != userId) return; // no active membership: fail closed
        if (!scope.Has(requirement.Permission)) return;

        if (http != null) http.Items[ClinicScopeExtensions.ItemKey] = scope;
        context.Succeed(requirement);
    }
}

public static class ClinicScopeExtensions
{
    public const string ItemKey = "MedFlow.ClinicScope";

    /// <summary>The scope the authorization handler resolved. Throws (-> 403) when an action was reached without one.</summary>
    public static ClinicScope GetScope(this HttpContext http) =>
        http.Items[ItemKey] as ClinicScope ?? throw new UnauthorizedAccessException("No clinic scope for this request.");

    public static ClinicScope GetScope(this ControllerBase controller) => controller.HttpContext.GetScope();
}
