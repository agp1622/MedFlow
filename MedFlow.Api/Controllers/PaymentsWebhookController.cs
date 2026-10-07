using System.Globalization;
using System.Net;
using System.Text.Json;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MedFlow.Api.Controllers;

/// <summary>
/// Receives Stripe webhooks. Anonymous by necessity: the request is trusted only if its
/// signature verifies against the configured webhook secret.
/// </summary>
[ApiController]
[Route("api/payments")]
[AllowAnonymous]
public class PaymentsWebhookController : ControllerBase
{
    private readonly IInvoiceRepository _invoices;
    private readonly IEmailSender _email;
    private readonly PaymentSettings _settings;
    private readonly ILogger<PaymentsWebhookController> _logger;

    public PaymentsWebhookController(IInvoiceRepository invoices, IEmailSender email,
        IOptions<PaymentSettings> settings, ILogger<PaymentsWebhookController> logger)
    {
        _invoices = invoices;
        _email = email;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        if (string.IsNullOrEmpty(_settings.WebhookSecret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        if (!StripeSignature.Verify(payload, Request.Headers["Stripe-Signature"], _settings.WebhookSecret, DateTimeOffset.UtcNow))
            return BadRequest();

        JsonElement session;
        try
        {
            var root = JsonDocument.Parse(payload).RootElement;
            if (root.GetProperty("type").GetString() != "checkout.session.completed") return Ok();
            session = root.GetProperty("data").GetProperty("object");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest();
        }

        if (session.TryGetProperty("payment_status", out var ps) && ps.GetString() != "paid") return Ok();
        if (!session.TryGetProperty("client_reference_id", out var refId) ||
            !int.TryParse(refId.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var invoiceId))
            return Ok();

        var invoice = await _invoices.GetWithPatientAsync(invoiceId);
        if (invoice == null)
        {
            _logger.LogWarning("Stripe payment for unknown invoice {InvoiceId}", invoiceId);
            return Ok();
        }
        // Duplicate deliveries (Stripe retries) must not pay or email twice
        if (invoice.Status == InvoiceStatus.Paid) return Ok();

        var paid = session.TryGetProperty("amount_total", out var at) && at.TryGetInt64(out var cents)
            ? cents / 100m : (decimal?)null;
        if (paid != invoice.Amount)
        {
            _logger.LogWarning("Stripe payment for invoice {InvoiceId} did not match the amount due; left unpaid", invoiceId);
            return Ok();
        }

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAmount = paid;
        invoice.PaidDate = DateTime.UtcNow;
        await _invoices.UpdateAsync(invoice);
        _logger.LogInformation("Invoice {InvoiceId} paid online", invoiceId);

        var to = invoice.Patient?.Email;
        if (!string.IsNullOrWhiteSpace(to))
        {
            try
            {
                await _email.SendAsync(to, $"Receipt for invoice {invoice.InvoiceNumber}",
                    $"<p>Hello {WebUtility.HtmlEncode(invoice.Patient!.FirstName)},</p>" +
                    $"<p>Thank you. We received your payment of <strong>{paid:0.00}</strong> for invoice " +
                    $"<strong>{WebUtility.HtmlEncode(invoice.InvoiceNumber)}</strong> " +
                    $"({WebUtility.HtmlEncode(invoice.ServiceDescription)}) on {invoice.PaidDate:yyyy-MM-dd}.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment receipt for invoice {InvoiceId}", invoiceId);
            }
        }
        return Ok();
    }
}
