using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Localization;

/// <summary>Resolves the language a client asked for and renders catalog messages in it.</summary>
public static class Localizer
{
    /// <summary>First supported language in Accept-Language quality order; Spanish when absent, malformed or unsupported.</summary>
    public static string ResolveLanguage(HttpRequest? request)
    {
        try
        {
            var accepted = request?.GetTypedHeaders().AcceptLanguage;
            if (accepted is { Count: > 0 })
            {
                foreach (var entry in accepted.OrderByDescending(a => a.Quality ?? 1.0))
                {
                    var tag = entry.Value.Value;
                    if (string.IsNullOrWhiteSpace(tag) || (entry.Quality ?? 1.0) <= 0) continue;
                    var primary = tag.Split('-', 2)[0].ToLowerInvariant();
                    if (Messages.SupportedLanguages.Contains(primary)) return primary;
                }
            }
        }
        catch (Exception)
        {
            // A malformed header must never break the request
        }
        return Messages.DefaultLanguage;
    }

    public static string Get(string language, string key, params object[] args)
    {
        if (!Messages.Catalog.TryGetValue(key, out var entry))
            entry = Messages.Catalog["Error.Unexpected"];
        var template = language == "en" ? entry.En : entry.Es;
        return args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
    }

    public static string Get(HttpContext? context, string key, params object[] args) =>
        Get(ResolveLanguage(context?.Request), key, args);
}

public static class LocalizationExtensions
{
    /// <summary>Message <paramref name="key"/> in the language of the current request.</summary>
    public static string T(this ControllerBase controller, string key, params object[] args) =>
        Localizer.Get(controller.HttpContext, key, args);

    /// <summary>Localized descriptions for Identity failures; unknown codes get a generic localized message.</summary>
    public static IEnumerable<string> IdentityMessages(this ControllerBase controller, IdentityResult result, int requiredPasswordLength = 8) =>
        result.Errors.Select(e => e.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordTooShort) => controller.T("Identity.PasswordTooShort", requiredPasswordLength),
            nameof(IdentityErrorDescriber.PasswordRequiresDigit) => controller.T("Identity.PasswordRequiresDigit"),
            nameof(IdentityErrorDescriber.PasswordRequiresUpper) => controller.T("Identity.PasswordRequiresUpper"),
            nameof(IdentityErrorDescriber.PasswordRequiresLower) => controller.T("Identity.PasswordRequiresLower"),
            nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) => controller.T("Identity.PasswordRequiresNonAlphanumeric"),
            nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) => controller.T("Identity.PasswordRequiresUniqueChars"),
            nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName) => controller.T("Identity.DuplicateEmail"),
            nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName) => controller.T("Identity.InvalidEmail"),
            _ => controller.T("Identity.Default"),
        }).Distinct().ToList();
}
