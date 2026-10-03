# Research: Printable Prescriptions

## R1. PDF library

- **Decision**: PDFsharp 6.2.4 (package `PDFsharp`, core build, no GDI/WPF), licence MIT (verified in the NuGet
  nuspec: `<license type="expression">MIT</license>`).
- **Rationale**: The MIT licence has no revenue or seat conditions, so it fits a clinical product of any size and
  adds no compliance tracking. It is pure managed code with no native dependencies, works on Linux containers and
  Windows, and targets .NET 8. Its only dependencies are Microsoft packages. Needs drawing code rather than a
  fluent layout, which is acceptable for one fixed, single-page document.
- **Alternatives considered**:
  - QuestPDF (2026.9.1): nicer layout API and bundled fonts, but its licence is custom source-available
    (`LICENSE.md`): the free Community tier is limited by organisation revenue and a commercial licence is needed
    above that. Not permissive, and it creates a future licensing obligation, so rejected.
  - HTML-to-PDF (headless browser/wkhtmltopdf): heavy native dependency in the container. Rejected.

## R2. Fonts

- **Decision**: PDFsharp core cannot find system fonts on Linux (verified: `No appropriate font found for family
  name 'Arial'`). Bundle DejaVu Sans Regular and Bold as embedded resources and register a small `IFontResolver`.
- **Rationale**: Deterministic output on any host, full Latin coverage (Spanish accents). The DejaVu/Bitstream Vera
  licence permits redistribution; its text ships next to the fonts.
- **Alternatives**: rely on host fonts (non-deterministic, fails in the Docker image); standard PDF base-14 fonts
  (not available in the core build, and limited to WinAnsi).

## R3. Scoping and audit order

- **Decision**: Repository returns data only when `Prescription.DoctorId == caller` and `Patient.DoctorId == caller`.
  Null maps to 404. Then render, then `this.AuditAsync(..., View, Prescription, id)`; a false result (patient not
  accessible) maps to 404 as well. Audit is written only when a document is about to be returned.
- **Rationale**: Matches `GetByPatient` (View + doctor scoping) and keeps refused requests out of the log.

## R4. Client download

- **Decision**: Call `api.get(..., { responseType: 'blob' })` (as the attachments download does), create an object URL
  and open it in a new tab so the browser PDF viewer can print it; fall back to download if the popup is blocked.
- **Rationale**: JWT travels in the header, never in a URL; no new patterns.

## R5. Not-valid documents

- **Decision**: Compute validity at render time: valid iff status is Active (or ExpiringSoon) and expiry date >= today (UTC).
  Otherwise draw a "NOT VALID - <reason>" banner.
