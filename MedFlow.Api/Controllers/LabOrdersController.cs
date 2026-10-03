using MedFlow.Api.Extensions;
using MedFlow.Api.Localization;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>Lab orders and manually entered results. Doctor-private, owner-scoped, audited.</summary>
[ApiController]
[Route("api/patients/{patientId:int}/labs")]
[Authorize(Roles = Roles.Doctor)]
public class LabOrdersController : ControllerBase
{
    public const int MaxOrdersPerPatient = 100;
    public const int MaxResultsPerOrder = 50;

    private readonly ILabOrderRepository _labs;
    private readonly IAuditService _audit;

    public LabOrdersController(ILabOrderRepository labs, IAuditService audit) { _labs = labs; _audit = audit; }

    private string DoctorId => User.GetUserId();
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static bool InFuture(DateOnly? d) => d.HasValue && d.Value > DateOnly.FromDateTime(DateTime.UtcNow);
    private Task<bool> Audit(int patientId, AuditAction action, int? orderId, IEnumerable<string>? fields = null) =>
        this.AuditAsync(_audit, patientId, action, AuditItemKind.LabOrder, orderId, fields);

    [HttpGet]
    public async Task<ActionResult<LabSummaryDto>> GetAll(int patientId)
    {
        if (!await Audit(patientId, AuditAction.View, null)) return NotFound();
        return Ok(await _labs.GetSummaryAsync(patientId, DoctorId));
    }

    [HttpPost]
    public async Task<ActionResult<LabOrderDto>> Create(int patientId, [FromBody] SaveLabOrderRequest req)
    {
        if (!await _labs.OwnsPatientAsync(patientId, DoctorId)) return NotFound();
        if (InFuture(req.OrderedDate)) return BadRequest(new { message = this.T("Lab.DateFuture") });
        if (await _labs.CountOrdersAsync(patientId, DoctorId) >= MaxOrdersPerPatient)
            return BadRequest(new { message = this.T("Lab.OrderLimit", MaxOrdersPerPatient) });

        var dto = await _labs.AddOrderAsync(new LabOrder
        {
            PatientId = patientId, DoctorId = DoctorId, TestName = req.TestName.Trim(), Notes = Clean(req.Notes),
            OrderedDate = req.OrderedDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
        });
        if (!await Audit(patientId, AuditAction.Change, dto.Id)) return NotFound();
        return Created(string.Empty, dto);
    }

    [HttpPut("{orderId:int}")]
    public async Task<ActionResult<LabOrderDto>> Update(int patientId, int orderId, [FromBody] SaveLabOrderRequest req)
    {
        var order = await _labs.GetOrderAsync(orderId, patientId, DoctorId);
        if (order == null) return NotFound();
        if (order.Status == LabOrderStatus.Cancelled) return Conflict(new { message = this.T("Lab.Cancelled") });
        if (InFuture(req.OrderedDate)) return BadRequest(new { message = this.T("Lab.DateFuture") });

        var before = AuditDiff.Snapshot(order);
        order.TestName = req.TestName.Trim();
        order.Notes = Clean(req.Notes);
        order.OrderedDate = req.OrderedDate ?? order.OrderedDate;
        var dto = await _labs.SaveOrderAsync(order);
        if (!await Audit(patientId, AuditAction.Change, orderId, AuditDiff.Changed(before, order))) return NotFound();
        return Ok(dto);
    }

    [HttpPost("{orderId:int}/cancel")]
    public async Task<ActionResult<LabOrderDto>> Cancel(int patientId, int orderId)
    {
        var order = await _labs.GetOrderAsync(orderId, patientId, DoctorId);
        if (order == null) return NotFound();
        if (order.Status == LabOrderStatus.Cancelled) return Conflict(new { message = this.T("Lab.Cancelled") });

        order.Status = LabOrderStatus.Cancelled;
        var dto = await _labs.SaveOrderAsync(order);
        if (!await Audit(patientId, AuditAction.Change, orderId, new[] { nameof(LabOrder.Status) })) return NotFound();
        return Ok(dto);
    }

    [HttpDelete("{orderId:int}")]
    public async Task<IActionResult> Delete(int patientId, int orderId)
    {
        var order = await _labs.GetOrderAsync(orderId, patientId, DoctorId);
        if (order == null) return NotFound();
        await _labs.DeleteOrderAsync(order);
        if (!await Audit(patientId, AuditAction.Change, orderId)) return NotFound();
        return NoContent();
    }

    // ── Results ───────────────────────────────────────────────────────────────
    [HttpPost("{orderId:int}/results")]
    public async Task<ActionResult<LabOrderDto>> AddResult(int patientId, int orderId, [FromBody] SaveLabResultRequest req)
    {
        var order = await _labs.GetOrderAsync(orderId, patientId, DoctorId);
        if (order == null) return NotFound();
        if (order.Status == LabOrderStatus.Cancelled) return Conflict(new { message = this.T("Lab.Cancelled") });
        if (RangeInvalid(req)) return BadRequest(new { message = this.T("Lab.RangeOrder") });
        if (order.Results.Count(r => !r.IsDeleted) >= MaxResultsPerOrder)
            return BadRequest(new { message = this.T("Lab.ResultLimit", MaxResultsPerOrder) });

        var dto = await _labs.AddResultAsync(order, new LabResult
        {
            AnalyteName = req.AnalyteName.Trim(), Value = req.Value!.Value, Unit = Clean(req.Unit),
            ReferenceLow = req.ReferenceLow, ReferenceHigh = req.ReferenceHigh
        });
        if (!await Audit(patientId, AuditAction.Change, orderId, new[] { "Results" })) return NotFound();
        return Created(string.Empty, dto);
    }

    [HttpPut("{orderId:int}/results/{resultId:int}")]
    public async Task<ActionResult<LabOrderDto>> UpdateResult(int patientId, int orderId, int resultId, [FromBody] SaveLabResultRequest req)
    {
        var order = await _labs.GetOrderAsync(orderId, patientId, DoctorId);
        var result = order?.Results.FirstOrDefault(r => r.Id == resultId && !r.IsDeleted);
        if (order == null || result == null) return NotFound();
        if (order.Status == LabOrderStatus.Cancelled) return Conflict(new { message = this.T("Lab.Cancelled") });
        if (RangeInvalid(req)) return BadRequest(new { message = this.T("Lab.RangeOrder") });

        var before = AuditDiff.Snapshot(result);
        result.AnalyteName = req.AnalyteName.Trim();
        result.Value = req.Value!.Value;
        result.Unit = Clean(req.Unit);
        result.ReferenceLow = req.ReferenceLow;
        result.ReferenceHigh = req.ReferenceHigh;
        var dto = await _labs.SaveResultAsync(order, result);
        if (!await Audit(patientId, AuditAction.Change, orderId, AuditDiff.Changed(before, result))) return NotFound();
        return Ok(dto);
    }

    [HttpDelete("{orderId:int}/results/{resultId:int}")]
    public async Task<ActionResult<LabOrderDto>> RemoveResult(int patientId, int orderId, int resultId)
    {
        var order = await _labs.GetOrderAsync(orderId, patientId, DoctorId);
        var result = order?.Results.FirstOrDefault(r => r.Id == resultId && !r.IsDeleted);
        if (order == null || result == null) return NotFound();
        if (order.Status == LabOrderStatus.Cancelled) return Conflict(new { message = this.T("Lab.Cancelled") });

        var dto = await _labs.RemoveResultAsync(order, result);
        if (!await Audit(patientId, AuditAction.Change, orderId, new[] { "Results" })) return NotFound();
        return Ok(dto);
    }

    private static bool RangeInvalid(SaveLabResultRequest r) =>
        r.ReferenceLow.HasValue && r.ReferenceHigh.HasValue && r.ReferenceLow.Value > r.ReferenceHigh.Value;
}
