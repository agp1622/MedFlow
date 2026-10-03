namespace MedFlow.Core;

/// <summary>The consent statement shown on the intake form. Bump Version whenever the text changes.</summary>
public static class IntakeConsent
{
    public const string Version = "2026-10-v1";
    public const string Text =
        "I confirm that the information I have provided is accurate to the best of my knowledge. " +
        "I consent to my practice collecting, storing and using this information, including my health information, " +
        "to provide me with care and to manage my appointments and billing. " +
        "I understand that typing my full name below acts as my electronic signature.";
}
