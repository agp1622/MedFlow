# Research: Vitals Trends

- **Charting library**: Decision: Recharts (already a dependency, unused). Alternatives: Chart.js/visx (new dependency, rejected).
- **Data source**: Decision: reuse `GET /api/vitalsigns/patient/{id}`; it returns the full list newest first, which is small per patient. Alternative: server-side `from`/`to` query parameters - rejected as unnecessary scope; revisit if lists grow large.
- **Date range semantics**: Decision: filter by local calendar day, inclusive. `recordedAt` is UTC (`DateTime.UtcNow`) serialized without guaranteed offset; the existing UI parses it with `parseISO`. Helper treats a timestamp lacking a zone designator as UTC (append `Z`) so charts are consistent. Risk noted: existing `fmt.date` does not do this; chart axis uses the same parsed instant converted to local time.
- **Blood pressure**: stored as free text. Decision: parse `^\s*(\d{2,3})\s*/\s*(\d{2,3})\s*$`; unparseable values are skipped.
- **Glucose**: no field; excluded and flagged (adding needs entity, DTO, migration, form).
- **Client tests**: no runner; adding vitest is a new dependency, rejected. Backend tests cover access control.
- **Audit**: each fetch of the endpoint logs a View audit event; the Overview tab already fetches it, and the Vitals tab shares the same TanStack query key, so no extra requests or audit noise.
