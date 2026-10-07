# API Contracts

## GET /api/invoices/{id}/claim-export?format=json|csv

Auth: JWT, role Doctor only (401 anonymous, 403 patient role).

| Case | Response |
|---|---|
| Invoice of own patient, format absent or `json` | 200 `application/json`, attachment `claim-draft-{invoiceNumber}.json` |
| `format=csv` | 200 `text/csv; charset=utf-8`, attachment `claim-draft-{invoiceNumber}.csv` |
| Other format | 400 ProblemDetails, localized title and message (checked before lookup) |
| Missing, soft-deleted, other doctor's invoice or patient | 404 empty body, nothing audited |
| Audit cannot be stored | 500 (fail closed), no data returned |

Headers: `Cache-Control: no-store`. Language from `Accept-Language` (default Spanish).

JSON body (`ClaimDraftDto`):

```json
{
  "status": "DRAFT",
  "disclaimer": "DRAFT ... not an official CMS-1500 form, not an X12 837 file, not validated ...",
  "generatedAt": "2026-10-03T12:00:00Z",
  "invoiceNumber": "INV-0001",
  "invoiceStatus": "Pending",
  "items": [ { "item": "1a", "key": "insured_id_number", "label": "...", "value": "ABC123" } ],
  "missing": [ { "item": "24D", "key": "procedure_cpt_hcpcs", "label": "..." } ]
}
```

CSV: UTF-8, header `section,item,key,label,value`. First rows: `status,,status,,DRAFT` and `disclaimer,,disclaimer,,<text>`; then one `item` row per item and one `missing` row per missing item (value empty). Fields quoted per RFC 4180; values beginning with `= + - @` tab or CR are prefixed with `'`.

Audit: View, item kind Invoice, invoice id, patient id; no values.

## Patient endpoints (changed)

`PatientDto`, `CreatePatientRequest`, `UpdatePatientRequest` gain optional `insuranceGroupNumber`, `insurancePayerId`, `insuranceSubscriberName`, `insuranceSubscriberDateOfBirth` (date), `insuranceSubscriberRelationship` (string enum). Invalid input: 400 ValidationProblemDetails with localized messages. Fields are optional so old clients keep working. Audit on update lists changed field names (existing behaviour).
