using Google.Apis.Auth;

namespace MedFlow.Api.Services;

/// <summary>Validates a Google ID token. A seam so tests can sign in "with Google" without reaching Google.</summary>
public interface IGoogleIdTokenValidator
{
    /// <exception cref="InvalidJwtException">The token is not valid for this application.</exception>
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(string credential, string clientId);
}

public class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(string credential, string clientId) =>
        GoogleJsonWebSignature.ValidateAsync(credential,
            new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });
}
