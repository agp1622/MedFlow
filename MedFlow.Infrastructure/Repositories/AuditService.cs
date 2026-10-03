using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Repositories;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) { _db = db; }

    public async Task<bool> RecordAsync(string actorUserId, string actorRole, int patientId, AuditAction action,
        AuditItemKind itemKind, int? itemId = null, IEnumerable<string>? changedFields = null)
    {
        // Owning doctor of the patient, or null when the patient is missing / not accessible to the actor
        var doctorId = actorRole == Roles.Patient
            ? await _db.Patients.AsNoTracking()
                .Where(p => p.Id == patientId && p.PortalUserId == actorUserId)
                .Select(p => p.DoctorId).FirstOrDefaultAsync()
            : await _db.Patients.AsNoTracking()
                .Where(p => p.Id == patientId && p.DoctorId == actorUserId)
                .Select(p => p.DoctorId).FirstOrDefaultAsync();
        if (doctorId == null) return false;

        var name = await _db.Users.AsNoTracking().Where(u => u.Id == actorUserId)
            .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync();

        var fields = changedFields?.Distinct().ToArray();
        _db.AuditEvents.Add(new AuditEvent
        {
            PatientId = patientId,
            DoctorId = doctorId,
            ActorUserId = actorUserId,
            ActorName = string.IsNullOrWhiteSpace(name) ? "Unknown" : name.Trim(),
            ActorRole = actorRole,
            Action = action,
            ItemKind = itemKind,
            ItemId = itemId,
            ChangedFields = fields is { Length: > 0 } ? string.Join(",", fields) : null,
            OccurredAt = DateTime.UtcNow
        });
        // Callers that mutate a tracked entity before recording have that change saved in this same call
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AuditEventDto>?> GetLogAsync(int patientId, string doctorId, AuditLogQuery q)
    {
        if (!await _db.Patients.AnyAsync(p => p.Id == patientId && p.DoctorId == doctorId)) return null;

        var query = _db.AuditEvents.AsNoTracking().Where(a => a.PatientId == patientId && a.DoctorId == doctorId);
        if (q.Action.HasValue) query = query.Where(a => a.Action == q.Action.Value);
        if (!string.IsNullOrWhiteSpace(q.Actor))
        {
            var actor = q.Actor.Trim().ToLower();
            query = query.Where(a => a.ActorName.ToLower().Contains(actor));
        }
        if (q.From.HasValue)
        {
            var from = DateTime.SpecifyKind(q.From.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.OccurredAt >= from);
        }
        if (q.To.HasValue)
        {
            var to = DateTime.SpecifyKind(q.To.Value.Date, DateTimeKind.Utc).AddDays(1);
            query = query.Where(a => a.OccurredAt < to);
        }

        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync();

        var items = rows.Select(a => new AuditEventDto(a.Id, a.OccurredAt, a.ActorUserId, a.ActorName, a.ActorRole,
            a.Action.ToString(), a.ItemKind.ToString(), a.ItemId,
            string.IsNullOrEmpty(a.ChangedFields) ? Array.Empty<string>() : a.ChangedFields.Split(',')));
        return new PagedResult<AuditEventDto>(items, total, q.Page, q.PageSize);
    }
}
