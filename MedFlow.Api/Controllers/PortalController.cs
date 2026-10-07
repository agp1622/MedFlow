using MedFlow.Api.Extensions;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>
/// Read-only patient portal. The patient is ALWAYS resolved from the authenticated user,
/// never from a client-supplied id, so a patient can only ever reach their own records.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Patient)]
public class PortalController : ControllerBase
{
    private readonly IPortalRepository _portal;
    private readonly IWebHostEnvironment _env;
    private readonly IPaymentGateway _payments;
    private readonly IConfiguration _config;
    private readonly ILogger<PortalController> _logger;

    public PortalController(IPortalRepository portal, IWebHostEnvironment env, IPaymentGateway payments,
        IConfiguration config, ILogger<PortalController> logger)
    {
        _portal = portal;
        _env = env;
        _payments = payments;
        _config = config;
        _logger = logger;
    }

    private async Task<Patient?> ResolvePatientAsync()
    {
        var patient = await _portal.GetPatientByUserIdAsync(User.GetUserId());
        return patient is { Status: PatientStatus.Active } ? patient : null;
    }

    private ObjectResult Unavailable() =>
        StatusCode(StatusCodes.Status403Forbidden, new { error = "Portal access is unavailable." });

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(await _portal.GetProfileAsync(patient));
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(await _portal.GetAppointmentsAsync(patient.Id));
    }

    [HttpGet("prescriptions")]
    public async Task<IActionResult> Prescriptions()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(await _portal.GetPrescriptionsAsync(patient.Id));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(await _portal.GetInvoicesAsync(patient.Id));
    }

    /// <summary>Starts an online payment for one of the patient's own unpaid invoices.</summary>
    [HttpPost("invoices/{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id)
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();

        // Missing and someone-else's look identical: 404
        var invoice = await _portal.GetOwnInvoiceAsync(id, patient.Id);
        if (invoice == null) return NotFound();
        if (invoice.Status is not (InvoiceStatus.Pending or InvoiceStatus.Overdue))
            return BadRequest(new { error = "This invoice is not payable." });
        if (invoice.Amount <= 0) return BadRequest(new { error = "This invoice is not payable." });
        if (!_payments.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Online payment is not available right now." });

        var frontendUrl = (_config.GetSection("AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:5173").TrimEnd('/');
        try
        {
            var url = await _payments.CreateCheckoutSessionAsync(new CheckoutRequest(
                invoice.Id, invoice.InvoiceNumber, invoice.ServiceDescription, invoice.Amount, patient.Email,
                $"{frontendUrl}/portal?payment=success", $"{frontendUrl}/portal?payment=cancelled"));
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not start checkout for invoice {InvoiceId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Could not start the payment. Please try again." });
        }
    }

    [HttpGet("attachments")]
    public async Task<IActionResult> Attachments()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(await _portal.GetSharedAttachmentsAsync(patient.Id));
    }

    [HttpGet("attachments/{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();

        // Missing, unshared and someone-else's all look identical: 404
        var attachment = await _portal.GetSharedAttachmentAsync(id, patient.Id);
        if (attachment == null) return NotFound();

        var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments",
            attachment.PatientId.ToString(), attachment.StoredFileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        await _portal.LogAccessAsync(patient.Id, "Attachment", new[] { id }, "Download");
        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, attachment.ContentType, attachment.FileName);
    }

    [HttpGet("notes")]
    public async Task<IActionResult> Notes()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        var notes = (await _portal.GetSharedNotesAsync(patient.Id)).ToList();
        if (notes.Count > 0)
            await _portal.LogAccessAsync(patient.Id, "Note", notes.Select(n => n.Id), "View");
        return Ok(notes);
    }
}
