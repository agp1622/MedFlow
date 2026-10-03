using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<MedicalNote> MedicalNotes => Set<MedicalNote>();
    public DbSet<NoteTemplate> NoteTemplates => Set<NoteTemplate>();
    public DbSet<PatientAttachment> PatientAttachments => Set<PatientAttachment>();
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
    public DbSet<PatientProblem> PatientProblems => Set<PatientProblem>();
    public DbSet<PatientMedication> PatientMedications => Set<PatientMedication>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<PortalInvitation> PortalInvitations => Set<PortalInvitation>();
    public DbSet<PortalAccessLog> PortalAccessLogs => Set<PortalAccessLog>();
    public DbSet<IntakeLink> IntakeLinks => Set<IntakeLink>();
    public DbSet<IntakeSubmission> IntakeSubmissions => Set<IntakeSubmission>();

    public DbSet<DoctorAvailability> DoctorAvailabilities => Set<DoctorAvailability>();
    public DbSet<DoctorBlockedDate> DoctorBlockedDates => Set<DoctorBlockedDate>();

    public DbSet<AppointmentReminder> AppointmentReminders => Set<AppointmentReminder>();
    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<WaitlistOffer> WaitlistOffers => Set<WaitlistOffer>();

    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<ClinicMember> ClinicMembers => Set<ClinicMember>();
    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global query filter - soft delete
        builder.Entity<Patient>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Appointment>().HasQueryFilter(a => !a.IsDeleted);
        builder.Entity<Prescription>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Invoice>().HasQueryFilter(i => !i.IsDeleted);
        builder.Entity<VitalSign>().HasQueryFilter(v => !v.IsDeleted);
        builder.Entity<MedicalNote>().HasQueryFilter(n => !n.IsDeleted);
        builder.Entity<NoteTemplate>().HasQueryFilter(t => !t.IsDeleted);
        builder.Entity<PatientAttachment>().HasQueryFilter(a => !a.IsDeleted);

        builder.Entity<PatientAllergy>().HasQueryFilter(a => !a.IsDeleted);
        builder.Entity<PatientProblem>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<PatientMedication>().HasQueryFilter(m => !m.IsDeleted);
        builder.Entity<LabOrder>().HasQueryFilter(o => !o.IsDeleted);
        builder.Entity<LabResult>().HasQueryFilter(r => !r.IsDeleted);

        // Patient
        builder.Entity<Patient>(e =>
        {
            e.HasIndex(p => p.Email);
            e.HasIndex(p => new { p.DoctorId, p.Status });
            e.HasIndex(p => p.PortalUserId).IsUnique().HasFilter("[PortalUserId] IS NOT NULL");
            e.Property(p => p.PortalUserId).HasMaxLength(450);
            e.Property(p => p.BloodType).HasConversion<string>();
            e.Property(p => p.Gender).HasConversion<string>();
            e.Property(p => p.Status).HasConversion<string>();
            e.Property(p => p.InsuranceGroupNumber).HasMaxLength(100);
            e.Property(p => p.InsurancePayerId).HasMaxLength(50);
            e.Property(p => p.InsuranceSubscriberName).HasMaxLength(200);
            e.Property(p => p.InsuranceSubscriberRelationship).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Email).HasMaxLength(256);
            e.Property(p => p.FirstName).HasMaxLength(100);
            e.Property(p => p.LastName).HasMaxLength(100);
            e.Ignore(p => p.FullName);
            e.Ignore(p => p.Age);
            e.HasOne(p => p.Doctor).WithMany(d => d.Patients)
                .HasForeignKey(p => p.DoctorId).HasPrincipalKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Doctor
        builder.Entity<Doctor>(e =>
        {
            e.HasIndex(d => d.UserId).IsUnique();
            e.Ignore(d => d.FullName);
        });

        // Online booking
        builder.Entity<DoctorAvailability>(e =>
        {
            e.HasQueryFilter(a => !a.IsDeleted);
            e.HasIndex(a => new { a.DoctorId, a.DayOfWeek });
            e.Property(a => a.DayOfWeek).HasConversion<string>();
        });
        builder.Entity<DoctorBlockedDate>(e =>
        {
            e.HasQueryFilter(b => !b.IsDeleted);
            e.HasIndex(b => new { b.DoctorId, b.Date });
            e.Property(b => b.Label).HasMaxLength(200);
        });

        // Appointment
        builder.Entity<Appointment>(e =>
        {
            e.HasIndex(a => new { a.DoctorId, a.ScheduledAt });
            e.Property(a => a.Type).HasConversion<string>();
            e.Property(a => a.Status).HasConversion<string>();
            e.HasOne(a => a.Patient).WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Doctor).WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId).HasPrincipalKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Prescription
        builder.Entity<Prescription>(e =>
        {
            e.HasIndex(p => new { p.PatientId, p.Status });
            e.Property(p => p.Status).HasConversion<string>();
            e.HasOne(p => p.Patient).WithMany(p => p.Prescriptions)
                .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Doctor).WithMany()
                .HasForeignKey(p => p.DoctorId).HasPrincipalKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Invoice
        builder.Entity<Invoice>(e =>
        {
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            e.HasIndex(i => new { i.DoctorId, i.Status });
            e.Property(i => i.Amount).HasPrecision(18, 2);
            e.Property(i => i.PaidAmount).HasPrecision(18, 2);
            e.Property(i => i.Status).HasConversion<string>();
            e.HasOne(i => i.Patient).WithMany(p => p.Invoices)
                .HasForeignKey(i => i.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Doctor).WithMany()
                .HasForeignKey(i => i.DoctorId).HasPrincipalKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // MedicalNote
        builder.Entity<MedicalNote>(e =>
        {
            e.Property(n => n.SharedWithPatient).HasDefaultValue(false);
            e.HasIndex(n => new { n.PatientId, n.DoctorId });
            e.HasOne(n => n.Patient).WithMany(p => p.MedicalNotes)
                .HasForeignKey(n => n.PatientId).OnDelete(DeleteBehavior.Restrict);
            // DoctorId is the author's user id; nurses author notes too, so there is deliberately no FK to Doctors
            e.Property(n => n.DoctorId).HasMaxLength(450);
        });

        // NoteTemplate
        builder.Entity<NoteTemplate>(e =>
        {
            e.Property(t => t.DoctorId).HasMaxLength(450).IsRequired();
            e.Property(t => t.Name).HasMaxLength(100).IsRequired();
            e.Property(t => t.Body).HasMaxLength(5000).IsRequired();
            e.HasIndex(t => t.DoctorId);
        });

        // VitalSign
        builder.Entity<VitalSign>(e =>
        {
            e.HasIndex(v => new { v.PatientId, v.RecordedAt });
            e.Property(v => v.Weight).HasPrecision(5, 2);
            e.Property(v => v.Height).HasPrecision(5, 2);
            e.Property(v => v.Bmi).HasPrecision(5, 2);
            e.Property(v => v.Temperature).HasPrecision(4, 1);
            e.HasOne(v => v.Patient).WithMany(p => p.VitalSigns)
                .HasForeignKey(v => v.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        // PatientAttachment
        builder.Entity<PatientAttachment>(e =>
        {
            e.HasIndex(a => new { a.PatientId, a.DoctorId });
            e.Property(a => a.FileName).HasMaxLength(500);
            e.Property(a => a.StoredFileName).HasMaxLength(500);
            e.Property(a => a.ContentType).HasMaxLength(200);
            e.Property(a => a.Category).HasMaxLength(100);
            e.Property(a => a.SharedWithPatient).HasDefaultValue(false);
            e.HasOne(a => a.Patient).WithMany(p => p.Attachments)
                .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        // Lab orders and results (doctor-facing only)
        builder.Entity<LabOrder>(e =>
        {
            e.HasIndex(o => new { o.PatientId, o.DoctorId });
            e.Property(o => o.DoctorId).HasMaxLength(450).IsRequired();
            e.Property(o => o.TestName).HasMaxLength(150).IsRequired();
            e.Property(o => o.Notes).HasMaxLength(1000);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(o => o.Patient).WithMany().HasForeignKey(o => o.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(o => o.Results).WithOne().HasForeignKey(r => r.LabOrderId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<LabResult>(e =>
        {
            e.HasIndex(r => r.LabOrderId);
            e.Property(r => r.AnalyteName).HasMaxLength(100).IsRequired();
            e.Property(r => r.Unit).HasMaxLength(30);
            e.Property(r => r.Value).HasPrecision(18, 4);
            e.Property(r => r.ReferenceLow).HasPrecision(18, 4);
            e.Property(r => r.ReferenceHigh).HasPrecision(18, 4);
        });

        // Clinical lists (doctor-facing only)
        builder.Entity<PatientAllergy>(e =>
        {
            e.HasIndex(a => new { a.PatientId, a.DoctorId });
            e.Property(a => a.Substance).HasMaxLength(200);
            e.Property(a => a.Reaction).HasMaxLength(500);
            e.Property(a => a.Severity).HasConversion<string>().HasMaxLength(20);
            e.HasOne(a => a.Patient).WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PatientProblem>(e =>
        {
            e.HasIndex(p => new { p.PatientId, p.DoctorId });
            e.Property(p => p.Description).HasMaxLength(200);
            e.Property(p => p.Icd10Code).HasMaxLength(8);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(p => p.Patient).WithMany().HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PatientMedication>(e =>
        {
            e.HasIndex(m => new { m.PatientId, m.DoctorId });
            e.Property(m => m.Name).HasMaxLength(200);
            e.Property(m => m.Dosage).HasMaxLength(100);
            e.Property(m => m.Frequency).HasMaxLength(100);
            e.Property(m => m.Notes).HasMaxLength(500);
            e.HasOne(m => m.Patient).WithMany().HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        // PortalInvitation (no navigation to Patient: Patient has a soft-delete query filter)
        builder.Entity<PortalInvitation>(e =>
        {
            e.HasIndex(i => i.TokenHash);
            e.HasIndex(i => i.PatientId);
            e.Property(i => i.Email).HasMaxLength(256);
            e.Property(i => i.TokenHash).HasMaxLength(64);
        });

        // IntakeLink / IntakeSubmission (no navigation to Patient: Patient has a soft-delete query filter)
        builder.Entity<IntakeLink>(e =>
        {
            e.HasIndex(i => i.TokenHash).IsUnique();
            e.HasIndex(i => i.PatientId);
            e.Property(i => i.Email).HasMaxLength(256);
            e.Property(i => i.TokenHash).HasMaxLength(64);
            e.Property(i => i.UsedAt).IsConcurrencyToken();
        });
        builder.Entity<IntakeSubmission>(e =>
        {
            e.HasIndex(s => new { s.DoctorId, s.Status, s.SubmittedAt });
            e.HasIndex(s => s.IntakeLinkId).IsUnique();
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Gender).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.DoctorId).HasMaxLength(450);
            e.Property(s => s.DecidedByDoctorId).HasMaxLength(450);
            e.Property(s => s.FirstName).HasMaxLength(100);
            e.Property(s => s.LastName).HasMaxLength(100);
            e.Property(s => s.Phone).HasMaxLength(30);
            e.Property(s => s.Address).HasMaxLength(200);
            e.Property(s => s.City).HasMaxLength(100);
            e.Property(s => s.State).HasMaxLength(100);
            e.Property(s => s.ZipCode).HasMaxLength(20);
            e.Property(s => s.InsuranceProvider).HasMaxLength(200);
            e.Property(s => s.InsurancePolicyNumber).HasMaxLength(100);
            e.Property(s => s.PrimaryCondition).HasMaxLength(500);
            e.Property(s => s.Allergies).HasMaxLength(1000);
            e.Property(s => s.CurrentMedications).HasMaxLength(2000);
            e.Property(s => s.PastHistory).HasMaxLength(2000);
            e.Property(s => s.AdditionalNotes).HasMaxLength(2000);
            e.Property(s => s.ConsentVersion).HasMaxLength(50);
            e.Property(s => s.SignatureName).HasMaxLength(200);
            e.Property(s => s.RejectionReason).HasMaxLength(500);
        });

        // AppointmentReminder / ReminderDelivery (no navigations: Appointment has a soft-delete query filter)
        builder.Entity<AppointmentReminder>(e =>
        {
            e.HasIndex(r => r.TokenHash);
            e.HasIndex(r => new { r.AppointmentId, r.ScheduledAt });
            e.Property(r => r.TokenHash).HasMaxLength(64);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Response).HasConversion<string>().HasMaxLength(20);
        });
        builder.Entity<ReminderDelivery>(e =>
        {
            e.HasIndex(d => d.AppointmentId);
            e.Property(d => d.Channel).HasMaxLength(20);
            e.Property(d => d.Outcome).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.Reason).HasMaxLength(200);
        });

        // Waitlist (no navigations: Patient/Appointment have soft-delete query filters)
        builder.Entity<WaitlistEntry>(e =>
        {
            e.HasIndex(w => new { w.DoctorId, w.Status, w.CreatedAt });
            e.HasIndex(w => new { w.PatientId, w.Status });
            e.Property(w => w.DoctorId).HasMaxLength(450);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
        });
        builder.Entity<WaitlistOffer>(e =>
        {
            e.HasIndex(o => new { o.EntryId, o.SlotStartsAt }).IsUnique();
            e.HasIndex(o => o.TokenHash);
            e.Property(o => o.TokenHash).HasMaxLength(64);
        });

        // PortalAccessLog
        builder.Entity<PortalAccessLog>(e =>
        {
            e.HasIndex(l => new { l.PatientId, l.OccurredAt });
            e.Property(l => l.ResourceType).HasMaxLength(50);
            e.Property(l => l.Action).HasMaxLength(50);
        });

        // AuditEvent (append-only; no FK to Patient so events outlive soft-deleted patients)
        builder.Entity<AuditEvent>(e =>
        {
            e.HasIndex(a => new { a.PatientId, a.OccurredAt });
            e.Property(a => a.DoctorId).HasMaxLength(450);
            e.Property(a => a.ActorUserId).HasMaxLength(450);
            e.Property(a => a.ActorName).HasMaxLength(200);
            e.Property(a => a.ActorRole).HasMaxLength(20);
            e.Property(a => a.Action).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.ItemKind).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.ChangedFields).HasMaxLength(500);
        });

        // Clinics, membership and staff invitations
        builder.Entity<Clinic>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.Stamp).IsConcurrencyToken();
        });
        builder.Entity<ClinicMember>(e =>
        {
            e.HasIndex(m => m.UserId).IsUnique();
            e.HasIndex(m => new { m.ClinicId, m.Role, m.IsActive });
            e.Property(m => m.UserId).HasMaxLength(450).IsRequired();
            e.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Clinic>().WithMany().HasForeignKey(m => m.ClinicId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<StaffInvitation>(e =>
        {
            e.HasIndex(i => i.TokenHash);
            e.HasIndex(i => new { i.ClinicId, i.Email });
            e.Property(i => i.Email).HasMaxLength(256).IsRequired();
            e.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.InvitedByUserId).HasMaxLength(450).IsRequired();
            e.HasOne<Clinic>().WithMany().HasForeignKey(i => i.ClinicId).OnDelete(DeleteBehavior.Restrict);
        });

        // Every clinic-scoped record: indexed ClinicId with a restricting FK to its clinic
        foreach (var type in builder.Model.GetEntityTypes().Where(t => typeof(IClinicScoped).IsAssignableFrom(t.ClrType)).ToList())
        {
            var entity = builder.Entity(type.ClrType);
            entity.HasIndex("ClinicId");
            entity.HasOne(typeof(Clinic)).WithMany().HasForeignKey("ClinicId").OnDelete(DeleteBehavior.Restrict);
        }
    }

    /// <summary>
    /// Central guarantee that no clinic-scoped record is ever saved without a clinic, in a different clinic than its
    /// patient, or by a doctor/author of another clinic. Rows added without a ClinicId take it from their patient (a
    /// patient from its treating doctor's membership). Anything that cannot be resolved or contradicts throws, so a
    /// writer that forgets the clinic fails instead of creating unscoped or cross-clinic data.
    /// </summary>
    private async Task StampClinicIdsAsync(CancellationToken ct)
    {
        var added = ChangeTracker.Entries().Where(e => e.State == EntityState.Added && e.Entity is IClinicScoped).ToList();
        if (added.Count == 0) return;

        var patientClinics = new Dictionary<int, int>();
        var memberClinics = new Dictionary<string, int?>();
        foreach (var entry in added)
        {
            var scoped = (IClinicScoped)entry.Entity;
            var name = entry.Metadata.ClrType.Name;

            if (entry.Entity is Patient patient)
            {
                if (scoped.ClinicId == 0)
                    scoped.ClinicId = await MemberClinicAsync(memberClinics, patient.DoctorId, ct)
                        ?? throw new InvalidOperationException("A patient needs a treating doctor that belongs to a clinic.");
            }
            else if (entry.Metadata.FindProperty("PatientId") == null)
            {
                if (scoped.ClinicId == 0) throw new InvalidOperationException($"{name} was saved without a clinic.");
            }
            else
            {
                var patientId = (int)entry.Property("PatientId").CurrentValue!;
                if (!patientClinics.TryGetValue(patientId, out var patientClinic))
                {
                    var stored = await Patients.IgnoreQueryFilters().AsNoTracking().Where(p => p.Id == patientId)
                        .Select(p => (int?)p.ClinicId).FirstOrDefaultAsync(ct);
                    stored ??= Patients.Local.Where(p => p.Id == patientId).Select(p => (int?)p.ClinicId).FirstOrDefault();
                    patientClinic = stored ?? 0;
                    patientClinics[patientId] = patientClinic;
                }
                if (patientClinic == 0)
                {
                    if (scoped.ClinicId == 0) throw new InvalidOperationException($"{name} was saved for a patient without a clinic.");
                }
                else if (scoped.ClinicId == 0) scoped.ClinicId = patientClinic;
                else if (scoped.ClinicId != patientClinic)
                    throw new InvalidOperationException($"{name} clinic differs from its patient's clinic.");
            }

            // The doctor or author of a record must belong to the record's clinic (a user without any membership is tolerated)
            if (entry.Metadata.FindProperty("DoctorId") != null
                && entry.Property("DoctorId").CurrentValue is string doctorId && doctorId.Length > 0)
            {
                var doctorClinic = await MemberClinicAsync(memberClinics, doctorId, ct);
                if (doctorClinic != null && doctorClinic != scoped.ClinicId)
                    throw new InvalidOperationException($"{name} was written by a user of another clinic.");
            }
        }
    }

    private async Task<int?> MemberClinicAsync(Dictionary<string, int?> cache, string userId, CancellationToken ct)
    {
        if (!cache.TryGetValue(userId, out var clinicId))
        {
            clinicId = await ClinicMembers.AsNoTracking().Where(m => m.UserId == userId)
                .Select(m => (int?)m.ClinicId).FirstOrDefaultAsync(ct);
            cache[userId] = clinicId;
        }
        return clinicId;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Audit events are append-only: refuse any attempt to edit or remove one
        if (ChangeTracker.Entries<AuditEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are immutable.");

        await StampClinicIdsAsync(ct);

        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entity.CreatedAt = DateTime.UtcNow;
        }
        return await base.SaveChangesAsync(ct);
    }
}
