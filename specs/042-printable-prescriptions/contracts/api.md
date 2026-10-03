# Contract: Prescription PDF

## GET /api/prescriptions/{id}/pdf

- Auth: JWT bearer, role `Doctor` (existing controller attribute). Patient token: 403. No token: 401.
- Success `200`: body is `application/pdf`, header `Content-Disposition: attachment; filename="prescription-{id}.pdf"`,
  `Cache-Control: no-store`. Side effect: audit event View / Prescription / item id = `{id}` / patient id.
- `404` with empty body: prescription missing, belongs to another doctor, or patient not the caller's. Identical in
  all cases; no audit event.

Client: `prescriptionsApi.downloadPdf(id): Promise<Blob>`.
