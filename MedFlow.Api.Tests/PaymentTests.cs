using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class PaymentTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public PaymentTests(TestApiFactory f) => _f = f;

    private async Task<(AuthResult Doctor, AuthResult Patient, int InvoiceId)> SetupAsync(
        string tag, InvoiceStatus status = InvoiceStatus.Pending, decimal amount = 25.50m)
    {
        var doctor = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, $"p-{tag}@x.com", tag);
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, $"p-{tag}@x.com");
        var invoiceId = await _f.WithDbAsync(async db =>
        {
            var inv = new Invoice { PatientId = pid, DoctorId = doctor.UserId, InvoiceNumber = "INV-" + tag,
                ServiceDescription = "Visit " + tag, Amount = amount, Status = status };
            db.Invoices.Add(inv);
            await db.SaveChangesAsync();
            return inv.Id;
        });
        return (doctor, patient, invoiceId);
    }

    private Task<Invoice> LoadAsync(int id) =>
        _f.WithDbAsync(db => db.Invoices.AsNoTracking().FirstAsync(i => i.Id == id));

    private Task<HttpResponseMessage> PostWebhookAsync(int invoiceId, long amountCents, string status = "paid",
        string? secret = null, DateTimeOffset? at = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_1", type = "checkout.session.completed",
            data = new { @object = new { id = "cs_1", client_reference_id = invoiceId.ToString(),
                payment_status = status, amount_total = amountCents } }
        });
        var ts = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var sig = StripeSignature.Sign(payload, ts, secret ?? TestApiFactory.WebhookSecret);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        req.Headers.Add("Stripe-Signature", $"t={ts},v1={sig}");
        return _f.CreateClient().SendAsync(req);
    }

    [Fact]
    public async Task Patient_can_start_checkout_for_their_own_unpaid_invoice()
    {
        var (_, patient, id) = await SetupAsync("pay1");
        var res = await _f.ClientFor(patient.Token).PostAsync($"/api/portal/invoices/{id}/checkout", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"https://checkout.test/pay/{id}", body.GetProperty("url").GetString());
        var sent = _f.Payments.Requests.Last(r => r.InvoiceId == id);
        Assert.Equal(25.50m, sent.Amount);
        Assert.Equal("p-pay1@x.com", sent.CustomerEmail);
    }

    [Fact]
    public async Task Patient_cannot_check_out_someone_elses_invoice()
    {
        var (_, _, otherInvoice) = await SetupAsync("pay2a");
        var (_, patient, _) = await SetupAsync("pay2b");
        var res = await _f.ClientFor(patient.Token).PostAsync($"/api/portal/invoices/{otherInvoice}/checkout", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.DoesNotContain(_f.Payments.Requests, r => r.InvoiceId == otherInvoice);
    }

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    [InlineData(InvoiceStatus.Draft)]
    public async Task Non_payable_invoices_are_rejected(InvoiceStatus status)
    {
        var (_, patient, id) = await SetupAsync($"pay3{status}", status);
        var res = await _f.ClientFor(patient.Token).PostAsync($"/api/portal/invoices/{id}/checkout", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Doctors_and_anonymous_callers_cannot_start_checkout()
    {
        var (doctor, _, id) = await SetupAsync("pay4");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _f.ClientFor(doctor.Token).PostAsync($"/api/portal/invoices/{id}/checkout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _f.ClientFor().PostAsync($"/api/portal/invoices/{id}/checkout", null)).StatusCode);
    }

    [Fact]
    public async Task Webhook_marks_invoice_paid_and_emails_one_receipt_even_if_redelivered()
    {
        var (_, _, id) = await SetupAsync("pay5");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(id, 2550)).StatusCode);
        var inv = await LoadAsync(id);
        Assert.Equal(InvoiceStatus.Paid, inv.Status);
        Assert.Equal(25.50m, inv.PaidAmount);
        Assert.NotNull(inv.PaidDate);

        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(id, 2550)).StatusCode);
        var receipts = _f.Email.Sent.Where(e => e.To == "p-pay5@x.com" && e.Subject.Contains("INV-pay5")).ToList();
        Assert.Single(receipts);
    }

    [Fact]
    public async Task Webhook_with_bad_or_stale_signature_is_rejected_and_changes_nothing()
    {
        var (_, _, id) = await SetupAsync("pay6");
        Assert.Equal(HttpStatusCode.BadRequest, (await PostWebhookAsync(id, 2550, secret: "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostWebhookAsync(id, 2550, at: DateTimeOffset.UtcNow.AddHours(-1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _f.CreateClient().PostAsync("/api/payments/webhook", new StringContent("{}"))).StatusCode);
        Assert.Equal(InvoiceStatus.Pending, (await LoadAsync(id)).Status);
    }

    [Fact]
    public async Task Webhook_ignores_unpaid_sessions_and_amount_mismatches()
    {
        var (_, _, id) = await SetupAsync("pay7");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(id, 2550, status: "unpaid")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(id, 100)).StatusCode);
        Assert.Equal(InvoiceStatus.Pending, (await LoadAsync(id)).Status);
    }

    [Fact]
    public async Task Checkout_is_unavailable_when_the_gateway_is_not_configured()
    {
        var (_, patient, id) = await SetupAsync("pay8");
        _f.Payments.IsConfigured = false;
        try
        {
            var res = await _f.ClientFor(patient.Token).PostAsync($"/api/portal/invoices/{id}/checkout", null);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        }
        finally { _f.Payments.IsConfigured = true; }
    }
}
