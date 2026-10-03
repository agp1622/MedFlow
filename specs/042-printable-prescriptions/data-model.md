# Data Model: Printable Prescriptions

No new tables, columns or migrations. Existing entities are read only.

## PrescriptionDocumentData (Core record, not persisted)

| Field | Source |
|---|---|
| PrescriptionId | Prescription.Id |
| DrugName, Dosage, Frequency, Instructions? | Prescription |
| IssuedDate, ExpiryDate, RefillsRemaining, Status | Prescription |
| DoctorName, Specialty, LicenseNumber?, DoctorPhone? | Doctor (name via `FullName`) |
| PatientName, DateOfBirth, PatientPhone, PatientAddress? | Patient (address assembled from Address, City, State, ZipCode when present) |

Deliberately excluded: patient email, insurance, allergies, notes, attachments (FR-010).
