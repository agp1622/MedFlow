using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;

namespace MedFlow.Core.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

public interface IPatientRepository : IRepository<Patient>
{
    Task<PagedResult<PatientSummaryDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<Patient?> GetWithDetailsAsync(int id, string doctorId);
    Task<DateTime?> GetLastVisitAsync(int patientId);
    Task<DateTime?> GetNextAppointmentAsync(int patientId);
    Task<IEnumerable<PatientSummaryDto>> GetRecentAsync(string doctorId, int count = 5);
}

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<PagedResult<AppointmentDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<IEnumerable<AppointmentDto>> GetTodayAsync(string doctorId);
    Task<IEnumerable<AppointmentDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<IEnumerable<AppointmentDto>> GetUpcomingAsync(string doctorId, int count = 5);
}

public interface IPrescriptionRepository : IRepository<Prescription>
{
    Task<PagedResult<PrescriptionDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<IEnumerable<PrescriptionDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<int> GetExpiringCountAsync(string doctorId, int daysAhead = 30);
    Task UpdateExpiryStatusesAsync();
}

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<PagedResult<InvoiceDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<IEnumerable<InvoiceDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<decimal> GetPendingAmountAsync(string doctorId);
    Task<int> GetOverdueCountAsync(string doctorId);
    Task UpdateOverdueStatusesAsync();
    Task<string> GenerateInvoiceNumberAsync();
}

public interface IVitalSignRepository : IRepository<VitalSign>
{
    Task<IEnumerable<VitalSignDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<VitalSignDto?> GetLatestByPatientAsync(int patientId);
}

public interface IMedicalNoteRepository : IRepository<MedicalNote>
{
    Task<IEnumerable<MedicalNoteDto>> GetByPatientAsync(int patientId, string doctorId);
}

public interface IDashboardRepository
{
    Task<DashboardStatsDto> GetStatsAsync(string doctorId);
}
