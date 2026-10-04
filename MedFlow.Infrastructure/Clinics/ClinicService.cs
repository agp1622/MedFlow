using System.Collections.Concurrent;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using MedFlow.Infrastructure.Reminders;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Clinics;

/// <summary>
/// Clinic membership and staff management. Every membership change of a clinic is serialized (in-process lock plus the
/// clinic's concurrency stamp for other instances) so the last active Owner can never be removed, even concurrently.
/// </summary>
public class ClinicService : IClinicService
{
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private const int MaxAttempts = 3;
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    private readonly AppDbContext _db;
    public ClinicService(AppDbContext db) { _db = db; }

    // ── Scope ─────────────────────────────────────────────────────────────────
    public async Task<ClinicScope?> ResolveScopeAsync(string userId)
    {
        var m = await ActiveMembership(userId).FirstOrDefaultAsync();
        return m == null ? null : new ClinicScope(m.ClinicId, userId, m.Role);
    }

    public async Task<ClinicMembershipInfo?> GetMembershipAsync(string userId)
    {
        var m = await ActiveMembership(userId).FirstOrDefaultAsync();
        return m == null ? null : new ClinicMembershipInfo(m.ClinicId, m.ClinicName, m.Role);
    }

    private IQueryable<MembershipRow> ActiveMembership(string userId) =>
        from m in _db.ClinicMembers.AsNoTracking()
        join c in _db.Clinics.AsNoTracking() on m.ClinicId equals c.Id
        where m.UserId == userId && m.IsActive && !m.IsDeleted && !c.IsDeleted
        select new MembershipRow(m.ClinicId, c.Name, m.Role);

    private sealed record MembershipRow(int ClinicId, string ClinicName, ClinicRole Role);

    public async Task<ClinicScope> ProvisionOwnerAsync(string userId, string displayName)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "Clinic" : $"Clinic of {displayName.Trim()}";
        var clinic = new Clinic { Name = name.Length > 200 ? name[..200] : name };
        _db.Clinics.Add(clinic);
        await _db.SaveChangesAsync();
        _db.ClinicMembers.Add(new ClinicMember { ClinicId = clinic.Id, UserId = userId, Role = ClinicRole.Owner, IsActive = true });
        await _db.SaveChangesAsync();
        return new ClinicScope(clinic.Id, userId, ClinicRole.Owner);
    }

    // ── Clinic ────────────────────────────────────────────────────────────────
    public async Task<ClinicDto?> GetClinicAsync(ClinicScope scope)
    {
        var c = await _db.Clinics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == scope.ClinicId && !x.IsDeleted);
        return c == null ? null : new ClinicDto(c.Id, c.Name, scope.Role.ToString(), scope.UserId);
    }

    public async Task<ClinicDto?> RenameAsync(ClinicScope scope, string name)
    {
        var clinic = await _db.Clinics.FirstOrDefaultAsync(x => x.Id == scope.ClinicId && !x.IsDeleted);
        if (clinic == null) return null;
        clinic.Name = name.Trim();
        await _db.SaveChangesAsync();
        return new ClinicDto(clinic.Id, clinic.Name, scope.Role.ToString(), scope.UserId);
    }

    public async Task<IReadOnlyList<ClinicDoctorDto>> GetDoctorsAsync(ClinicScope scope)
    {
        var rows = await (from m in _db.ClinicMembers.AsNoTracking()
                          join d in _db.Doctors.AsNoTracking() on m.UserId equals d.UserId
                          where m.ClinicId == scope.ClinicId && m.IsActive && !m.IsDeleted
                                && (m.Role == ClinicRole.Owner || m.Role == ClinicRole.Doctor)
                          orderby m.CreatedAt, m.Id
                          select new { d.UserId, d.FirstName, d.LastName, m.Role }).ToListAsync();
        return rows.Select(r => new ClinicDoctorDto(r.UserId, $"{r.FirstName} {r.LastName}".Trim(), r.Role.ToString())).ToList();
    }

    public async Task<string?> ResolveTreatingDoctorAsync(ClinicScope scope, string? requestedDoctorId)
    {
        var doctors = await GetDoctorsAsync(scope);
        if (!string.IsNullOrWhiteSpace(requestedDoctorId))
            return doctors.Any(d => d.UserId == requestedDoctorId) ? requestedDoctorId : null;
        if (scope.IsClinician && doctors.Any(d => d.UserId == scope.UserId)) return scope.UserId;
        return doctors.FirstOrDefault(d => d.Role == nameof(ClinicRole.Owner))?.UserId
               ?? doctors.FirstOrDefault()?.UserId;
    }

    // ── Staff lists ───────────────────────────────────────────────────────────
    public async Task<PagedResult<StaffMemberDto>> ListStaffAsync(ClinicScope scope, QueryParams q)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var query = from m in _db.ClinicMembers.AsNoTracking()
                    join u in _db.Users.AsNoTracking() on m.UserId equals u.Id
                    where m.ClinicId == scope.ClinicId && !m.IsDeleted
                    select new { m, u };
        var total = await query.CountAsync();
        var rows = await query.OrderBy(x => x.m.CreatedAt).ThenBy(x => x.m.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return new PagedResult<StaffMemberDto>(rows.Select(r => ToDto(r.m, r.u.Email ?? "", r.u.FirstName, r.u.LastName)).ToList(), total, page, size);
    }

    public async Task<PagedResult<StaffInvitationDto>> ListInvitationsAsync(ClinicScope scope, QueryParams q)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var now = DateTime.UtcNow;
        var query = _db.StaffInvitations.AsNoTracking()
            .Where(i => i.ClinicId == scope.ClinicId && i.UsedAt == null && i.ExpiresAt >= now);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return new PagedResult<StaffInvitationDto>(
            rows.Select(i => new StaffInvitationDto(i.Id, i.Email, i.Role.ToString(), i.ExpiresAt, i.CreatedAt)).ToList(), total, page, size);
    }

    private static StaffMemberDto ToDto(ClinicMember m, string email, string first, string last) =>
        new(m.Id, m.UserId, email, first, last, m.Role.ToString(), m.IsActive, m.CreatedAt);

    private async Task<StaffMemberDto> LoadDtoAsync(ClinicMember m)
    {
        var u = await _db.Users.AsNoTracking().FirstAsync(x => x.Id == m.UserId);
        return ToDto(m, u.Email ?? "", u.FirstName, u.LastName);
    }

    // ── Invitations ───────────────────────────────────────────────────────────
    public async Task<(string Token, DateTime ExpiresAt)?> InviteAsync(ClinicScope scope, string email, ClinicRole role)
    {
        if (role == ClinicRole.Owner) throw new ArgumentException("Invitations cannot grant the Owner role.", nameof(role));
        var clean = email.Trim();
        var normalized = clean.ToUpperInvariant();
        // An address that already has an account is never invited (callers must not reveal why)
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalized)) return null;

        var now = DateTime.UtcNow;
        var pending = await _db.StaffInvitations
            .Where(i => i.ClinicId == scope.ClinicId && i.Email.ToUpper() == normalized && i.UsedAt == null).ToListAsync();
        foreach (var p in pending) p.UsedAt = now;

        var token = ReminderTokens.Generate();
        var invitation = new StaffInvitation
        {
            ClinicId = scope.ClinicId, Email = clean, Role = role, TokenHash = ReminderTokens.Hash(token),
            ExpiresAt = now.Add(InvitationLifetime), InvitedByUserId = scope.UserId
        };
        _db.StaffInvitations.Add(invitation);
        await _db.SaveChangesAsync();
        return (token, invitation.ExpiresAt);
    }

    public async Task<bool> RevokeInvitationAsync(ClinicScope scope, int invitationId)
    {
        var inv = await _db.StaffInvitations.FirstOrDefaultAsync(i =>
            i.Id == invitationId && i.ClinicId == scope.ClinicId && i.UsedAt == null && i.ExpiresAt >= DateTime.UtcNow);
        if (inv == null) return false;
        inv.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<StaffInvitation?> FindValidInvitationAsync(string token, string email)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200 || string.IsNullOrWhiteSpace(email)) return null;
        var hash = ReminderTokens.Hash(token);
        var inv = await _db.StaffInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash);
        if (inv == null || inv.UsedAt != null || inv.ExpiresAt < DateTime.UtcNow || inv.IsDeleted) return null;
        if (!string.Equals(inv.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
        if (!await _db.Clinics.AnyAsync(c => c.Id == inv.ClinicId && !c.IsDeleted)) return null;
        return inv;
    }

    public async Task<bool> AcceptInvitationAsync(StaffInvitation invitation, string userId, string firstName, string lastName, string specialty)
    {
        var gate = Locks.GetOrAdd(invitation.ClinicId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var tracked = await _db.StaffInvitations.FirstAsync(i => i.Id == invitation.Id);
            if (tracked.UsedAt != null) return false;
            if (await _db.ClinicMembers.AnyAsync(m => m.UserId == userId)) return false;
            tracked.UsedAt = DateTime.UtcNow;
            _db.ClinicMembers.Add(new ClinicMember { ClinicId = tracked.ClinicId, UserId = userId, Role = tracked.Role, IsActive = true });
            if (tracked.Role is ClinicRole.Doctor or ClinicRole.Owner)
                await EnsureDoctorProfileAsync(userId, firstName, lastName, specialty);
            await RotateStampAsync(tracked.ClinicId);
            await _db.SaveChangesAsync();
            return true;
        }
        finally { gate.Release(); }
    }

    // ── Membership changes (last-owner rule) ──────────────────────────────────
    public Task<(StaffChangeOutcome, StaffMemberDto?)> ChangeRoleAsync(ClinicScope scope, int memberId, ClinicRole role) =>
        MutateAsync(scope, memberId, async member =>
        {
            if (member.Role == ClinicRole.Owner && member.IsActive && role != ClinicRole.Owner
                && !await AnotherActiveOwnerAsync(member))
                return StaffChangeOutcome.LastOwner;
            member.Role = role;
            if (role is ClinicRole.Owner or ClinicRole.Doctor) await EnsureProfileFromUserAsync(member.UserId);
            return StaffChangeOutcome.Done;
        });

    public Task<(StaffChangeOutcome, StaffMemberDto?)> SetActiveAsync(ClinicScope scope, int memberId, bool active) =>
        MutateAsync(scope, memberId, async member =>
        {
            if (!active && member.Role == ClinicRole.Owner && member.IsActive && !await AnotherActiveOwnerAsync(member))
                return StaffChangeOutcome.LastOwner;
            member.IsActive = active;
            return StaffChangeOutcome.Done;
        });

    private Task<bool> AnotherActiveOwnerAsync(ClinicMember member) =>
        _db.ClinicMembers.AnyAsync(m => m.ClinicId == member.ClinicId && m.Id != member.Id
            && m.Role == ClinicRole.Owner && m.IsActive && !m.IsDeleted);

    private async Task<(StaffChangeOutcome, StaffMemberDto?)> MutateAsync(
        ClinicScope scope, int memberId, Func<ClinicMember, Task<StaffChangeOutcome>> change)
    {
        var gate = Locks.GetOrAdd(scope.ClinicId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                var member = await _db.ClinicMembers.FirstOrDefaultAsync(m =>
                    m.Id == memberId && m.ClinicId == scope.ClinicId && !m.IsDeleted);
                if (member == null) return (StaffChangeOutcome.NotFound, null);

                var outcome = await change(member);
                if (outcome != StaffChangeOutcome.Done) return (outcome, null);
                await RotateStampAsync(scope.ClinicId);
                try
                {
                    await _db.SaveChangesAsync();
                    return (StaffChangeOutcome.Done, await LoadDtoAsync(member));
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
                {
                    // Another instance changed this clinic's membership: reload and re-check the rule
                    _db.ChangeTracker.Clear();
                }
            }
        }
        finally { gate.Release(); }
    }

    private async Task RotateStampAsync(int clinicId)
    {
        var clinic = await _db.Clinics.FirstAsync(c => c.Id == clinicId);
        clinic.Stamp = Guid.NewGuid();
    }

    // ── Doctor profiles ───────────────────────────────────────────────────────
    private async Task EnsureProfileFromUserAsync(string userId)
    {
        var u = await _db.Users.AsNoTracking().FirstAsync(x => x.Id == userId);
        await EnsureDoctorProfileAsync(userId, u.FirstName, u.LastName, u.Specialty);
    }

    private async Task EnsureDoctorProfileAsync(string userId, string firstName, string lastName, string specialty)
    {
        if (await _db.Doctors.AnyAsync(d => d.UserId == userId)) return;
        _db.Doctors.Add(new Doctor
        {
            UserId = userId, FirstName = firstName, LastName = lastName,
            Specialty = string.IsNullOrWhiteSpace(specialty) ? "General Practice" : specialty
        });
    }
}
