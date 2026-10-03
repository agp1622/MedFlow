using System.Globalization;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;

namespace MedFlow.Core;

/// <summary>
/// Arranges the data MedFlow holds under CMS-1500 (02/12) item numbers and reports what is missing.
/// The result is a draft worksheet only: it is not the CMS-1500 form and not an X12 837 file.
/// CPT/HCPCS, ICD-10, provider NPI and federal tax ID are not stored, so they are always missing.
/// </summary>
public static class ClaimDraftBuilder
{
    public static ClaimDraft Build(ClaimSourceData d)
    {
        var self = d.SubscriberRelationship == InsuranceRelationship.Self;
        var patientName = $"{d.PatientLastName}, {d.PatientFirstName}".Trim(' ', ',');
        var insuredName = Clean(d.SubscriberName) ?? (self ? Clean(patientName) : null);
        var insuredDob = d.SubscriberDateOfBirth ?? (self ? d.PatientDateOfBirth : (DateOnly?)null);
        var serviceDate = d.AppointmentDate ?? d.InvoiceDate;
        var charge = d.Amount.ToString("0.00", CultureInfo.InvariantCulture);

        var rows = new (string Item, string Key, string? Value, bool Required)[]
        {
            ("Carrier", "payer_name", Clean(d.InsuranceProvider), true),
            ("Carrier", "payer_id", Clean(d.InsurancePayerId), true),
            ("1a", "insured_id_number", Clean(d.InsurancePolicyNumber), true),
            ("2", "patient_name", Clean(patientName), true),
            ("3", "patient_birth_date", Date(d.PatientDateOfBirth), true),
            ("3", "patient_sex", d.PatientGender switch { Gender.Male => "M", Gender.Female => "F", _ => null }, true),
            ("5", "patient_address", Clean(d.Address), true),
            ("5", "patient_city", Clean(d.City), true),
            ("5", "patient_state", Clean(d.State), true),
            ("5", "patient_zip", Clean(d.ZipCode), true),
            ("5", "patient_phone", Clean(d.PatientPhone), false),
            ("6", "patient_relationship_to_insured", d.SubscriberRelationship?.ToString(), true),
            ("4", "insured_name", insuredName, true),
            ("11", "insured_policy_group", Clean(d.InsuranceGroupNumber), false),
            ("11a", "insured_birth_date", insuredDob == null ? null : Date(insuredDob.Value), true),
            ("21", "diagnosis_icd10", null, true),
            ("24A", "service_date", Date(DateOnly.FromDateTime(serviceDate)), true),
            ("24D", "procedure_cpt_hcpcs", null, true),
            ("24F", "service_charge", charge, true),
            ("24G", "service_units", "1", false),
            ("24J/33a", "provider_npi", null, true),
            ("25", "federal_tax_id", null, true),
            ("28", "total_charge", charge, true),
            ("31/33", "billing_provider_name", Clean(d.DoctorName), true),
            ("33", "billing_provider_phone", Clean(d.DoctorPhone), false),
            ("-", "service_description", Clean(d.ServiceDescription), false),
            ("-", "invoice_number", Clean(d.InvoiceNumber), false),
            ("-", "invoice_status", Clean(d.InvoiceStatus), false),
        };

        var items = rows.Select(r => new ClaimItem(r.Item, r.Key, r.Value)).ToList();
        var missing = rows.Where(r => r.Required && r.Value == null).Select(r => new ClaimMissing(r.Item, r.Key)).ToList();
        return new ClaimDraft(items, missing);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
