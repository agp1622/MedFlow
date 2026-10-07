# Data Model

## Patient (new nullable columns)

| Property | Column | Rule |
|---|---|---|
| InsuranceGroupNumber | nvarchar(100) | max 100 |
| InsurancePayerId | nvarchar(50) | max 50 |
| InsuranceSubscriberName | nvarchar(200) | max 200 |
| InsuranceSubscriberDateOfBirth | date | not in the future, not before 1900-01-01 |
| InsuranceSubscriberRelationship | nvarchar(16), string-converted enum | one of Self, Spouse, Child, Other |

Existing, reused: InsuranceProvider (200), InsurancePolicyNumber (100). Blank strings are stored as null. Existing rows get nulls. Not exposed in portal DTOs or intake DTOs.

## Enum

`InsuranceRelationship { Self, Spouse, Child, Other }`

## Computed (not stored)

`ClaimSourceData`: invoice id/number/status/amount/description/date, appointment date (nullable), patient (name, DOB, gender, phone, address parts, insurance fields), doctor (full name, phone), patient id.

`ClaimDraftDto`: `status` ("DRAFT"), `disclaimer`, `generatedAt`, `invoiceNumber`, `invoiceStatus`, `items[]` (`item`, `key`, `label`, `value`), `missing[]` (`item`, `key`, `label`).

## Item map (CMS-1500 02/12 numbering, best effort)

| Item | Key | Source |
|---|---|---|
| Carrier | payer_name | InsuranceProvider |
| Carrier | payer_id | InsurancePayerId |
| 1a | insured_id_number | InsurancePolicyNumber |
| 2 | patient_name | "LAST, FIRST" |
| 3 | patient_birth_date | DOB (yyyy-MM-dd) |
| 3 | patient_sex | M/F |
| 5 | patient_address, patient_city, patient_state, patient_zip, patient_phone | Patient |
| 6 | patient_relationship_to_insured | InsuranceSubscriberRelationship |
| 4 | insured_name | subscriber name (patient name if Self and empty) |
| 11 | insured_policy_group | InsuranceGroupNumber |
| 11a | insured_birth_date | subscriber DOB (patient DOB if Self and empty) |
| 21 | diagnosis_icd10 | always missing |
| 24A | service_date | appointment date if linked else invoice date |
| 24D | procedure_cpt_hcpcs | always missing |
| 24F | service_charge | invoice amount |
| 24G | service_units | 1 |
| 24J / 33a | provider_npi | always missing |
| 25 | federal_tax_id | always missing |
| 28 | total_charge | invoice amount |
| 31/33 | billing_provider_name, billing_provider_phone | Doctor |
| (info) | service_description, invoice_number, invoice_status | Invoice (not CMS boxes; item "-") |

Required-and-missing rule: any of payer_name, payer_id, insured_id_number, patient_address (and city/state/zip), patient_sex, patient_relationship_to_insured, insured_name, insured_birth_date that is empty, plus the five always-missing keys.
