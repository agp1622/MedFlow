namespace MedFlow.Infrastructure.Payments;

public class PaymentSettings
{
    public const string SectionName = "Payments";

    /// <summary>Stripe secret key (sk_...). Supply via environment / user-secrets, never commit it.</summary>
    public string SecretKey { get; set; } = string.Empty;
    /// <summary>Stripe webhook signing secret (whsec_...).</summary>
    public string WebhookSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "usd";
}
