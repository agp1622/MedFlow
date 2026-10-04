using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Repositories;

public class LabOrderRepository : ILabOrderRepository
{
    private readonly AppDbContext _db;
    public LabOrderRepository(AppDbContext db) { _db = db; }

    public Task<bool> OwnsPatientAsync(int patientId, ClinicScope scope) =>
        _db.Patients.AnyAsync(p => p.Id == patientId && p.ClinicId == scope.ClinicId);

    public async Task<LabSummaryDto> GetSummaryAsync(int patientId, ClinicScope scope)
    {
        var orders = await _db.LabOrders.AsNoTracking().Include(o => o.Results)
            .Where(o => o.PatientId == patientId && o.ClinicId == scope.ClinicId)
            .OrderByDescending(o => o.OrderedDate).ThenByDescending(o => o.Id).ToListAsync();
        var dtos = orders.Select(ToDto).ToList();
        return new LabSummaryDto(dtos, dtos.Sum(o => o.AbnormalCount));
    }

    public Task<LabOrder?> GetOrderAsync(int orderId, int patientId, ClinicScope scope) =>
        _db.LabOrders.Include(o => o.Results)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.PatientId == patientId && o.ClinicId == scope.ClinicId);

    public Task<int> CountOrdersAsync(int patientId, ClinicScope scope) =>
        _db.LabOrders.CountAsync(o => o.PatientId == patientId && o.ClinicId == scope.ClinicId);

    public async Task<LabOrderDto> AddOrderAsync(LabOrder order)
    {
        _db.LabOrders.Add(order);
        await _db.SaveChangesAsync();
        return ToDto(order);
    }

    public async Task<LabOrderDto> SaveOrderAsync(LabOrder order)
    {
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(order);
    }

    public async Task DeleteOrderAsync(LabOrder order)
    {
        order.IsDeleted = true;
        foreach (var r in order.Results) r.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<LabOrderDto> AddResultAsync(LabOrder order, LabResult result)
    {
        order.Results.Add(result);
        Rederive(order);
        await _db.SaveChangesAsync();
        return ToDto(order);
    }

    public async Task<LabOrderDto> SaveResultAsync(LabOrder order, LabResult result)
    {
        result.UpdatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(order);
    }

    public async Task<LabOrderDto> RemoveResultAsync(LabOrder order, LabResult result)
    {
        result.IsDeleted = true;
        Rederive(order);
        await _db.SaveChangesAsync();
        return ToDto(order);
    }

    /// <summary>Ordered/Completed follows whether results exist; Cancelled is terminal.</summary>
    private static void Rederive(LabOrder order)
    {
        order.UpdatedAt = DateTime.UtcNow;
        if (order.Status == LabOrderStatus.Cancelled) return;
        order.Status = order.Results.Any(r => !r.IsDeleted) ? LabOrderStatus.Completed : LabOrderStatus.Ordered;
    }

    public static LabOrderDto ToDto(LabOrder o)
    {
        var results = o.Results.Where(r => !r.IsDeleted).OrderBy(r => r.Id)
            .Select(r => new LabResultDto(r.Id, r.AnalyteName, r.Value, r.Unit, r.ReferenceLow, r.ReferenceHigh, r.Flag.ToString()))
            .ToList();
        // Values on a cancelled order do not count as abnormal
        var abnormal = o.Status == LabOrderStatus.Cancelled ? 0 : results.Count(r => r.Flag != nameof(LabFlag.None));
        return new LabOrderDto(o.Id, o.TestName, o.Notes, o.OrderedDate, o.Status.ToString(), results, abnormal, o.CreatedAt, o.UpdatedAt);
    }
}
