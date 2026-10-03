# Quickstart: Printable Prescriptions

Prerequisites: API and client running with a seeded doctor that has a patient and a prescription.

1. Sign in as the doctor, open Prescriptions, click the printer icon on a row. A PDF opens in a new tab (or downloads).
   Check it shows doctor, patient, medication details and a blank signature line with printed name and date.
2. Open the audit log for that patient: a View entry for the prescription appears.
3. Print an expired or cancelled prescription: the page shows a "NOT VALID" banner.
4. As a second doctor, `GET /api/prescriptions/{id}/pdf` for the first doctor's prescription: 404, no audit entry.
5. With a patient token: 403.

Automated: `dotnet test MedFlow.Api.Tests --filter PrescriptionPdf`. Items 1, 3 (visual layout) and the browser
print flow need a manual pass; tests assert the file is a PDF and key text is present.
