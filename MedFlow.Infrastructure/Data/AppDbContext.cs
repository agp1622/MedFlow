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

        // Patient
        builder.Entity<Patient>(e =>
        {
            e.HasIndex(p => p.Email);
            e.HasIndex(p => new { p.DoctorId, p.Status });
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
