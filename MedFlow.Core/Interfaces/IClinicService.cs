using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;

namespace MedFlow.Core.Interfaces;

/// <summary>The active clinic membership of a user, as shown to them at sign-in.</summary>
public record ClinicMembershipInfo(int ClinicId, string ClinicName, ClinicRole Role);

public interface IClinicService
{
    /// <summary>The scope of the user's active membership of an existing clinic; null for no or inactive membership (fail closed).</summary>
    Task<ClinicScope?> ResolveScopeAsync(string userId);
    Task<ClinicMembershipInfo?> GetMembershipAsync(string userId);

    /// <summary>Creates a clinic named after the user and makes them its active Owner.</summary>
    Task<ClinicScope> ProvisionOwnerAsync(string userId, string displayName);

    Task<ClinicDto?> GetClinicAsync(ClinicScope scope);
    Task<ClinicDto?> RenameAsync(ClinicScope scope, string name);
    /// <summary>Active Owners and Doctors of the clinic that have a doctor profile (can be a treating doctor).</summary>
    Task<IReadOnlyList<ClinicDoctorDto>> GetDoctorsAsync(ClinicScope scope);
    /// <summary>
    /// The doctor user id to link a new patient/appointment/invoice to: the requested one when it is a clinic doctor,
    /// else the caller when they are a clinician, else the clinic's earliest active Owner. Null when the requested id is not a clinic doctor.
    /// </summary>
    Task<string?> ResolveTreatingDoctorAsync(ClinicScope scope, string? requestedDoctorId);

    Task<PagedResult<StaffMemberDto>> ListStaffAsync(ClinicScope scope, QueryParams query);
    Task<PagedResult<StaffInvitationDto>> ListInvitationsAsync(ClinicScope scope, QueryParams query);
    /// <summary>
    /// Creates an invitation (superseding earlier pending ones for the email) and returns the raw token.
    /// Null when the email already belongs to an account (callers answer exactly as for success).
    /// </summary>
    Task<(string Token, DateTime ExpiresAt)?> InviteAsync(ClinicScope scope, string email, ClinicRole role);
    Task<bool> RevokeInvitationAsync(ClinicScope scope, int invitationId);
    Task<(StaffChangeOutcome Outcome, StaffMemberDto? Member)> ChangeRoleAsync(ClinicScope scope, int memberId, ClinicRole role);
    Task<(StaffChangeOutcome Outcome, StaffMemberDto? Member)> SetActiveAsync(ClinicScope scope, int memberId, bool active);

    /// <summary>The invitation for a valid (unused, unexpired, email-matching) token; null for every other case.</summary>
    Task<StaffInvitation?> FindValidInvitationAsync(string token, string email);
    /// <summary>Consumes the invitation and joins the user to the clinic with the invited role; false if it was already used.</summary>
    Task<bool> AcceptInvitationAsync(StaffInvitation invitation, string userId, string firstName, string lastName, string specialty);
}
