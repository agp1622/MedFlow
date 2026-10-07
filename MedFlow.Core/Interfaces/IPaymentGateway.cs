namespace MedFlow.Core.Interfaces;

public record CheckoutRequest(
    int InvoiceId, string InvoiceNumber, string Description, decimal Amount,
    string? CustomerEmail, string SuccessUrl, string CancelUrl);

public interface IPaymentGateway
{
    /// <summary>True when the gateway has the credentials it needs to take payments.</summary>
    bool IsConfigured { get; }

    /// <summary>Creates a hosted checkout session and returns the URL to send the patient to.</summary>
    Task<string> CreateCheckoutSessionAsync(CheckoutRequest request);
}
