# Permission Matrix

Single source of truth: `MedFlow.Core.PermissionMatrix`. Enforced by `HasPermissionAttribute` + `PermissionAuthorizationHandler`. Denied permission -> 403; other clinic's or missing record -> 404; no/inactive membership or Patient token -> 403 on every staff endpoint; anonymous -> 401.

| Permission | Covers | Owner | Doctor | Nurse | Receptionist |
|---|---|:-:|:-:|:-:|:-:|
| ClinicRead | `GET /api/clinic`, `GET /api/clinic/doctors` | yes | yes | yes | yes |
| DashboardRead | `GET /api/dashboard` (figures for denied areas are zeroed) | yes | yes | yes | yes |
| PatientsRead | list/get patients (demographics, insurance) | yes | yes | yes | yes |
| PatientsWrite | create/update patient | yes | yes | no | yes |
| PatientsDelete | delete patient | yes | yes | no | no |
| PatientClinicalFields | primary condition, allergies, notes, blood type on the patient | yes | yes | yes | no |
| AppointmentsRead | list/get appointments, today, upcoming, reminder log | yes | yes | yes | yes |
| AppointmentsWrite | create/update/status/delete appointment | yes | yes | no | yes |
| WaitlistManage | list/add/remove waitlist | yes | yes | no | yes |
| AvailabilityManage | own weekly availability and blocked dates | yes | yes | no | no |
| IntakeLinkSend | send intake link | yes | yes | no | yes |
| IntakeReview | list/view/accept/reject intake submissions (contain clinical answers) | yes | yes | no | no |
| PortalInvite | send/revoke portal invitation | yes | yes | no | yes |
| PrescriptionsRead | list, by patient, PDF | yes | yes | yes | no |
| PrescriptionsWrite | create/update/delete prescription | yes | yes | no | no |
| InvoicesRead | list, by patient, claim export | yes | yes | no | yes |
| InvoicesWrite | create/update/mark-paid/delete invoice | yes | yes | no | yes |
| VitalsRead | list/latest vitals | yes | yes | yes | no |
| VitalsWrite | record vitals | yes | yes | yes | no |
| NotesRead | list/latest notes | yes | yes | yes | no |
| NotesWrite | create note | yes | yes | yes | no |
| NotesManage | share with patient, delete note | yes | yes | no | no |
| NoteTemplates | personal note templates | yes | yes | yes | no |
| AttachmentsRead | list, download, preview | yes | yes | yes | no |
| AttachmentsWrite | upload, share, delete | yes | yes | no | no |
| ClinicalListsRead | allergies, problems, medications | yes | yes | yes | no |
| ClinicalListsWrite | add/update/delete those | yes | yes | no | no |
| LabsRead | lab orders and results | yes | yes | yes | no |
| LabsWrite | order, result, cancel, delete | yes | yes | no | no |
| AuditLogRead | patient audit log (Doctor: only patients whose treating doctor they are) | yes | yes* | no | no |
| ReportsRead | revenue, visits, no-shows, AR aging and exports (Owner clinic-wide, Doctor own data) | yes | yes* | no | no |
| StaffManage | staff list, invitations, role change, deactivate/reactivate, rename clinic | yes | no | no | no |

`*` = scoped to the caller's own data.

Always unaffected: patient (portal) endpoints (`Patient` role), and public token endpoints (intake form, reminder responses, waitlist offers, online booking).
