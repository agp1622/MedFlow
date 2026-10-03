# Data Model: Lab Orders and Results

## LabOrder (table LabOrders, soft delete via query filter)
| Field | Type | Rules |
|-------|------|-------|
| Id, CreatedAt, UpdatedAt, IsDeleted | BaseEntity | |
| PatientId | int FK Patient (Restrict) | required |
| DoctorId | string(450) | owner; every query filters on it |
| TestName | string(150) | required, trimmed |
| Notes | string(1000)? | trimmed, empty -> null |
| OrderedDate | DateOnly | not in the future, defaults to today |
| Status | enum LabOrderStatus stored as string(20) | Ordered, Completed, Cancelled |
Index: (PatientId, DoctorId).

## LabResult (table LabResults, soft delete via query filter)
| Field | Type | Rules |
|-------|------|-------|
| LabOrderId | int FK LabOrder (Cascade not used; Restrict) | required |
| AnalyteName | string(100) | required, trimmed |
| Value | decimal(18,4) | required, within +/-999,999,999 |
| Unit | string(30)? | optional |
| ReferenceLow, ReferenceHigh | decimal(18,4)? | optional; low <= high when both present |
Index: LabOrderId. Flag is derived: Low if Value < ReferenceLow, High if Value > ReferenceHigh, else None.

## State
Ordered --(first result added)--> Completed --(last result removed)--> Ordered. Ordered or Completed --(cancel)--> Cancelled (no new, edited or removed results; order can still be deleted).

## Limits
100 orders per patient, 50 results per order.
