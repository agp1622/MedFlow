# Contract: Labs API

Base: `/api/patients/{patientId}/labs`. `[Authorize(Roles = Doctor)]`; patient tokens get 403. Non-owned patient or order -> 404. Enums are strings. Validation failure -> 400. Cancelled-order write -> 409.

| Verb | Route | Body | Success | Audit |
|------|-------|------|---------|-------|
| GET | `/` | - | 200 `LabSummaryDto` | View LabOrder |
| POST | `/` | `SaveLabOrderRequest` | 201 `LabOrderDto` | Change LabOrder |
| PUT | `/{orderId}` | `SaveLabOrderRequest` | 200 `LabOrderDto` | Change (field names) |
| POST | `/{orderId}/cancel` | - | 200 `LabOrderDto` | Change (`Status`) |
| DELETE | `/{orderId}` | - | 204 | Change |
| POST | `/{orderId}/results` | `SaveLabResultRequest` | 201 `LabOrderDto` | Change (`Results`) |
| PUT | `/{orderId}/results/{resultId}` | `SaveLabResultRequest` | 200 `LabOrderDto` | Change (field names) |
| DELETE | `/{orderId}/results/{resultId}` | - | 200 `LabOrderDto` | Change (`Results`) |

```
LabSummaryDto   { orders: LabOrderDto[], abnormalCount: int }   // newest first; Cancelled orders excluded from the count
LabOrderDto     { id, testName, notes?, orderedDate, status, results: LabResultDto[], abnormalCount, createdAt, updatedAt }
LabResultDto    { id, analyteName, value, unit?, referenceLow?, referenceHigh?, flag: "None"|"Low"|"High" }
SaveLabOrderRequest  { testName (req, <=150), notes? (<=1000), orderedDate? (not future) }
SaveLabResultRequest { analyteName (req, <=100), value (req), unit? (<=30), referenceLow?, referenceHigh? }
```
