using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using MedFlow.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace MedFlow.Infrastructure.Payments;

public class StripePaymentGateway : IPaymentGateway
{
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("https://api.stripe.com/") };
    private readonly PaymentSettings _settings;

    public StripePaymentGateway(IOptions<PaymentSettings> settings) => _settings = settings.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.SecretKey);

    public async Task<string> CreateCheckoutSessionAsync(CheckoutRequest r)
    {
        var form = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["client_reference_id"] = r.InvoiceId.ToString(CultureInfo.InvariantCulture),
            ["metadata[invoiceId]"] = r.InvoiceId.ToString(CultureInfo.InvariantCulture),
            ["success_url"] = r.SuccessUrl,
            ["cancel_url"] = r.CancelUrl,
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = _settings.Currency,
            ["line_items[0][price_data][unit_amount]"] = ToMinorUnits(r.Amount).ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = $"Invoice {r.InvoiceNumber}",
            ["line_items[0][price_data][product_data][description]"] = r.Description,
        };
        if (!string.IsNullOrWhiteSpace(r.CustomerEmail)) form["customer_email"] = r.CustomerEmail;

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/checkout/sessions") { Content = new FormUrlEncodedContent(form) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);
        using var res = await Http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Stripe checkout session failed ({(int)res.StatusCode}).");
        return JsonDocument.Parse(body).RootElement.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("Stripe returned no checkout URL.");
    }

    public static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
