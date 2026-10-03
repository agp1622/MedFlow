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
    public DbSet<PatientAttachment> PatientAttachments => Set<PatientAttachment>();
    public DbSet<PortalInvitation> PortalInvitations => Set<PortalInvitation>();
    public DbSet<PortalAccessLog> PortalAccessLogs => Set<PortalAccessLog>();
    public DbSet<AppointmentReminder> AppointmentReminders => Set<AppointmentReminder>();
    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();

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
        builder.Entity<PatientAttachment>().HasQueryFilter(a => !a.IsDeleted);

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
            e.HasOne(n => n.Doctor).WithMany()
                .HasForeignKey(n => n.DoctorId).HasPrincipalKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
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

        // PortalInvitation (no navigation to Patient: Patient has a soft-delete query filter)
        builder.Entity<PortalInvitation>(e =>
        {
            e.HasIndex(i => i.TokenHash);
            e.HasIndex(i => i.PatientId);
            e.Property(i => i.Email).HasMaxLength(256);
            e.Property(i => i.TokenHash).HasMaxLength(64);
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

        // PortalAccessLog
        builder.Entity<PortalAccessLog>(e =>
        {
            e.HasIndex(l => new { l.PatientId, l.OccurredAt });
            e.Property(l => l.ResourceType).HasMaxLength(50);
            e.Property(l => l.Action).HasMaxLength(50);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entity.CreatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}
