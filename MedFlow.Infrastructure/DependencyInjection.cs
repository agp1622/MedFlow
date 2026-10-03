using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using MedFlow.Infrastructure.Documents;
using MedFlow.Infrastructure.Email;
using MedFlow.Infrastructure.Identity;
using MedFlow.Infrastructure.Reminders;
using MedFlow.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
                          .EnableRetryOnFailure(3)));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<EmailSettings>(config.GetSection(EmailSettings.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.Configure<ReminderSettings>(config.GetSection(ReminderSettings.SectionName));
        services.AddScoped<IReminderProcessor, ReminderProcessor>();
        services.AddScoped<IReminderRepository, ReminderRepository>();

        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        services.AddSingleton<IPrescriptionDocumentRenderer, PrescriptionPdfRenderer>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IVitalSignRepository, VitalSignRepository>();
        services.AddScoped<IMedicalNoteRepository, MedicalNoteRepository>();
        services.AddScoped<INoteTemplateRepository, NoteTemplateRepository>();
        services.AddScoped<IPatientAttachmentRepository, PatientAttachmentRepository>();
        services.AddScoped<IPatientClinicalRepository, PatientClinicalRepository>();
        services.AddScoped<ILabOrderRepository, LabOrderRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPortalRepository, PortalRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPortalInvitationRepository, PortalInvitationRepository>();
        services.AddScoped<IIntakeRepository, IntakeRepository>();

        return services;
    }
}
