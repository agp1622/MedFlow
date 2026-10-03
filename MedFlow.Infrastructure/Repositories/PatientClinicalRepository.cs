using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Repositories;

public class PatientClinicalRepository : IPatientClinicalRepository
{
    private readonly AppDbContext _db;
    public PatientClinicalRepository(AppDbContext db) { _db = db; }

    public Task<bool> OwnsPatientAsync(int patientId, ClinicScope scope) =>
        _db.Patients.AnyAsync(p => p.Id == patientId && p.ClinicId == scope.ClinicId);

    public async Task<ClinicalSummaryDto> GetSummaryAsync(int patientId, ClinicScope scope)
    {
        var allergies = await _db.PatientAllergies
            .Where(a => a.PatientId == patientId && a.ClinicId == scope.ClinicId)
            .OrderByDescending(a => a.Id).ToListAsync();
        var problems = await _db.PatientProblems
            .Where(p => p.PatientId == patientId && p.ClinicId == scope.ClinicId)
            .OrderByDescending(p => p.Id).ToListAsync();
        var meds = await _db.PatientMedications
            .Where(m => m.PatientId == patientId && m.ClinicId == scope.ClinicId)
            .OrderByDescending(m => m.Id).ToListAsync();

        return new ClinicalSummaryDto(
            allergies.Select(a => new AllergyDto(a.Id, a.Substance, a.Reaction, a.Severity.ToString(), a.CreatedAt, a.UpdatedAt)),
            problems.Select(p => new ProblemDto(p.Id, p.Description, p.Icd10Code, p.Status.ToString(), p.OnsetDate, p.CreatedAt, p.UpdatedAt)),
            meds.Select(m => new MedicationDto(m.Id, m.Name, m.Dosage, m.Frequency, m.Notes, m.CreatedAt, m.UpdatedAt)));
    }

    public Task<T?> GetOwnedAsync<T>(int id, int patientId, ClinicScope scope) where T : ClinicalEntry =>
        _db.Set<T>().FirstOrDefaultAsync(e => e.Id == id && e.PatientId == patientId && e.ClinicId == scope.ClinicId);

    public Task<int> CountAsync<T>(int patientId, ClinicScope scope) where T : ClinicalEntry =>
        _db.Set<T>().CountAsync(e => e.PatientId == patientId && e.ClinicId == scope.ClinicId);

    public async Task<bool> AllergyExistsAsync(int patientId, ClinicScope scope, string substance, int? exceptId = null)
    {
        var key = substance.Trim().ToLower();
        return await _db.PatientAllergies.AnyAsync(a => a.PatientId == patientId && a.ClinicId == scope.ClinicId
            && a.Id != exceptId && a.Substance.ToLower() == key);
    }

    public async Task<T> AddAsync<T>(T entry) where T : ClinicalEntry
    {
        _db.Set<T>().Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task UpdateAsync<T>(T entry) where T : ClinicalEntry
    {
        _db.Set<T>().Update(entry);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync<T>(T entry) where T : ClinicalEntry
    {
        entry.IsDeleted = true;
        await _db.SaveChangesAsync();
    }
}
