namespace MedFlow.Api.Configuration;

/// <summary>
/// Fails fast at startup when the JWT signing key is missing or, outside Development/Testing,
/// a placeholder or too short. Messages never include the key itself.
/// </summary>
public static class JwtKeyValidator
{
    public const int MinimumLength = 32;

    private static readonly string[] PlaceholderMarkers =
        ["change", "your", "placeholder", "replace", "example", "secret"];

    public static void Validate(string? key, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Supply it via the Jwt__Key environment variable, a secret store, or user-secrets.");

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            return;

        if (key.Length < MinimumLength)
            throw new InvalidOperationException(
                $"Jwt:Key must be at least {MinimumLength} characters outside Development.");

        if (PlaceholderMarkers.Any(m => key.Contains(m, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Jwt:Key looks like a placeholder value. Configure a real signing key.");
    }
}
