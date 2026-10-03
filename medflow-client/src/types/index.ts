// ── Auth ──────────────────────────────────────────────────────────────────────
export interface LoginRequest { email: string; password: string }
export interface GoogleLoginRequest { credential: string }
export interface RegisterRequest {
  email: string; password: string
  firstName: string; lastName: string; specialty: string
}
export interface ForgotPasswordRequest { email: string }
export interface ResetPasswordRequest {
  email: string; token: string; newPassword: string; confirmPassword: string
}
export interface AuthResponse {
  token: string; refreshToken: string; expires: string
  user: UserDto
}
export interface UserDto {
  id: string; email: string; firstName: string; lastName: string; specialty: string
  role?: UserRole
}
export type UserRole = 'Doctor' | 'Patient'
export interface AcceptInvitationRequest {
  token: string; email: string; password: string; confirmPassword: string
}
export interface InvitationResult { message: string; expiresAt: string }

// ── Pagination ────────────────────────────────────────────────────────────────
export interface PagedResult<T> {
  items: T[]; totalCount: number; page: number; pageSize: number
  totalPages: number; hasNext: boolean; hasPrev: boolean
}
export interface QueryParams {
  page?: number; pageSize?: number; search?: string
  sortBy?: string; sortDesc?: boolean
}

// ── Enums ─────────────────────────────────────────────────────────────────────
export type PatientStatus = 'Active' | 'Inactive' | 'Deceased'
export type AppointmentStatus = 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow'
export type AppointmentType = 'NewPatient' | 'FollowUp' | 'CheckUp' | 'Consultation' | 'LabReview' | 'Emergency'
export type PrescriptionStatus = 'Active' | 'Expired' | 'Cancelled' | 'ExpiringSoon'
export type InvoiceStatus = 'Draft' | 'Pending' | 'Paid' | 'Overdue' | 'Cancelled'
export type Gender = 'Male' | 'Female' | 'NonBinary' | 'PreferNotToSay'
export type BloodType = 'APos' | 'ANeg' | 'BPos' | 'BNeg' | 'ABPos' | 'ABNeg' | 'OPos' | 'ONeg' | 'Unknown'

// ── Patient ───────────────────────────────────────────────────────────────────
export interface PatientDto {
  id: number; firstName: string; lastName: string; fullName: string
  dateOfBirth: string; age: number; gender: string; bloodType: string
  status: PatientStatus; email: string; phone: string
  address?: string; city?: string; state?: string; zipCode?: string
  primaryCondition?: string; allergies?: string; notes?: string
  insuranceProvider?: string; insurancePolicyNumber?: string
  insuranceGroupNumber?: string; insurancePayerId?: string; insuranceSubscriberName?: string
  insuranceSubscriberDateOfBirth?: string; insuranceSubscriberRelationship?: InsuranceRelationship
  lastVisit?: string; nextAppointment?: string
  createdAt: string; updatedAt: string
  portalStatus?: PortalStatus
}
export type InsuranceRelationship = 'Self' | 'Spouse' | 'Child' | 'Other'
export type PortalStatus = 'NotInvited' | 'Invited' | 'Active'
export interface PatientSummaryDto {
  id: number; fullName: string; age: number; gender: string; bloodType: string
  status: PatientStatus; email: string; phone: string
  primaryCondition?: string; lastVisit?: string; nextAppointment?: string
}
export interface CreatePatientRequest {
  firstName: string; lastName: string; dateOfBirth: string
  gender: Gender; bloodType: BloodType
  email: string; phone: string
  address?: string; city?: string; state?: string; zipCode?: string
  primaryCondition?: string; allergies?: string; notes?: string
  insuranceProvider?: string; insurancePolicyNumber?: string
  insuranceGroupNumber?: string; insurancePayerId?: string; insuranceSubscriberName?: string
  insuranceSubscriberDateOfBirth?: string; insuranceSubscriberRelationship?: InsuranceRelationship
}
export type UpdatePatientRequest = CreatePatientRequest & { status: PatientStatus }

// ── Appointment ───────────────────────────────────────────────────────────────
export interface AppointmentDto {
  id: number; patientId: number; patientName: string
  scheduledAt: string; durationMinutes: number
  type: string; status: AppointmentStatus
  reason?: string; notes?: string; location?: string; createdAt: string
}
export interface CreateAppointmentRequest {
  patientId: number; scheduledAt: string; durationMinutes: number
  type: AppointmentType; reason?: string; location?: string
}
export interface UpdateAppointmentRequest extends CreateAppointmentRequest {
  status: AppointmentStatus; notes?: string
}

// ── Prescription ──────────────────────────────────────────────────────────────
export interface PrescriptionDto {
  id: number; patientId: number; patientName: string
  drugName: string; dosage: string; frequency: string
  instructions?: string; issuedDate: string; expiryDate: string
  refillsRemaining: number; status: PrescriptionStatus; createdAt: string
}
export interface CreatePrescriptionRequest {
  patientId: number; drugName: string; dosage: string; frequency: string
  instructions?: string; issuedDate: string; expiryDate: string; refillsRemaining: number
}
export interface UpdatePrescriptionRequest {
  drugName: string; dosage: string; frequency: string
  instructions?: string; expiryDate: string
  refillsRemaining: number; status: PrescriptionStatus
}

// ── Invoice ───────────────────────────────────────────────────────────────────
export interface InvoiceDto {
  id: number; patientId: number; patientName: string
  appointmentId?: number; invoiceNumber: string
  serviceDescription: string; amount: number; paidAmount?: number
  status: InvoiceStatus; invoiceDate: string; dueDate?: string
  paidDate?: string; notes?: string; createdAt: string
}
export interface CreateInvoiceRequest {
  patientId: number; appointmentId?: number
  serviceDescription: string; amount: number
  dueDate?: string; notes?: string
}
export interface UpdateInvoiceRequest {
  serviceDescription: string; amount: number
  status: InvoiceStatus; dueDate?: string
  paidAmount?: number; notes?: string
}

// ── VitalSign ─────────────────────────────────────────────────────────────────
export interface VitalSignDto {
  id: number; patientId: number; recordedAt: string
  bloodPressure?: string; heartRate?: number; weight?: number
  height?: number; bmi?: number; temperature?: number
  oxygenSaturation?: number; recordedBy?: string
}
export interface CreateVitalSignRequest {
  patientId: number; bloodPressure?: string; heartRate?: number
  weight?: number; height?: number; temperature?: number; oxygenSaturation?: number
}

// ── MedicalNote ───────────────────────────────────────────────────────────────
export interface MedicalNoteDto {
  id: number; patientId: number; doctorName: string
  content: string; visitType?: string; noteDate: string
  sharedWithPatient?: boolean
}
export interface CreateMedicalNoteRequest {
  patientId: number; content: string; visitType?: string
}

// ── Clinical lists (allergies, problems, medications) ────────────────────────
export type AllergySeverity = 'Mild' | 'Moderate' | 'Severe' | 'LifeThreatening'
export type ProblemStatus = 'Active' | 'Resolved'
export interface AllergyDto {
  id: number; substance: string; reaction?: string | null; severity: AllergySeverity
  createdAt: string; updatedAt: string
}
export interface ProblemDto {
  id: number; description: string; icd10Code: string; status: ProblemStatus
  onsetDate?: string | null; createdAt: string; updatedAt: string
}
export interface MedicationDto {
  id: number; name: string; dosage?: string | null; frequency?: string | null; notes?: string | null
  createdAt: string; updatedAt: string
}
export interface ClinicalSummaryDto {
  allergies: AllergyDto[]; problems: ProblemDto[]; medications: MedicationDto[]
}
export interface SaveAllergyRequest { substance: string; reaction?: string; severity: AllergySeverity }
export interface SaveProblemRequest { description: string; icd10Code: string; status: ProblemStatus; onsetDate?: string }
export interface SaveMedicationRequest { name: string; dosage?: string; frequency?: string; notes?: string }
// ── NoteTemplate ──────────────────────────────────────────────────────────────
export interface NoteTemplateDto {
  id: number; name: string; body: string; isBuiltIn: boolean; updatedAt?: string
}
export interface CreateNoteTemplateRequest { name: string; body: string }
export type UpdateNoteTemplateRequest = CreateNoteTemplateRequest
export interface CopyForwardDto {
  noteId: number; content: string; visitType?: string; noteDate: string
}

// ── PatientAttachment ─────────────────────────────────────────────────────────
export interface PatientAttachmentDto {
  id: number; patientId: number; fileName: string; contentType: string
  fileSize: number; category?: string; description?: string; createdAt: string
  sharedWithPatient?: boolean
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
export interface DashboardStatsDto {
  totalPatients: number; activePatients: number
  todayAppointments: number; upcomingAppointments: number
  activePrescriptions: number; expiringPrescriptions: number
  pendingInvoicesAmount: number; overdueInvoices: number
  todaySchedule: AppointmentDto[]
  recentPatients: PatientSummaryDto[]
}

// ── Appointment reminders ─────────────────────────────────────────────────────
export type ReminderAction = 'Confirm' | 'Cancel'
export interface ReminderLookupDto {
  appointmentAt: string; durationMinutes: number; doctorName: string; location?: string
  status: AppointmentStatus; canRespond: boolean
}
export interface ReminderDeliveryDto { attemptedAt: string; channel: string; outcome: 'Sent' | 'Failed' | 'Skipped'; reason?: string }
export interface ReminderLogDto { response: 'None' | 'Confirmed' | 'Cancelled'; respondedAt?: string; deliveries: ReminderDeliveryDto[] }

// ── Patient portal ────────────────────────────────────────────────────────────
export interface PortalProfileDto { firstName: string; lastName: string; doctorName: string }
export interface PortalAppointmentDto {
  id: number; scheduledAt: string; durationMinutes: number; type: string; status: string
  reason?: string; location?: string
}
export interface PortalPrescriptionDto {
  id: number; drugName: string; dosage: string; frequency: string; instructions?: string
  issuedDate: string; expiryDate: string; refillsRemaining: number; status: string
}
export interface PortalInvoiceDto {
  id: number; invoiceNumber: string; serviceDescription: string; amount: number; paidAmount?: number
  status: string; invoiceDate: string; dueDate?: string; paidDate?: string
}
export interface PortalAttachmentDto {
  id: number; fileName: string; contentType: string; fileSize: number
  category?: string; description?: string; createdAt: string
}
export interface PortalNoteDto {
  id: number; doctorName: string; visitType?: string; content: string; noteDate: string
}

// ── Intake forms ──────────────────────────────────────────────────────────────
export type IntakeStatus = 'Pending' | 'Accepted' | 'Rejected'
export interface IntakeFormInfo { firstName: string; consentVersion: string; consentText: string }
export interface IntakeSubmitRequest {
  firstName: string; lastName: string; dateOfBirth: string; gender: Gender; phone: string
  address?: string; city?: string; state?: string; zipCode?: string
  insuranceProvider?: string; insurancePolicyNumber?: string
  primaryCondition?: string; allergies?: string
  currentMedications?: string; pastHistory?: string; additionalNotes?: string
  consentAgreed: boolean; signatureName: string
}
export interface IntakeSubmissionSummary {
  id: number; patientId: number; patientName: string; status: IntakeStatus; submittedAt: string
}
export interface IntakeAnswers {
  firstName: string; lastName: string; dateOfBirth: string; gender: string; phone: string
  address?: string | null; city?: string | null; state?: string | null; zipCode?: string | null
  insuranceProvider?: string | null; insurancePolicyNumber?: string | null
  primaryCondition?: string | null; allergies?: string | null
  currentMedications?: string | null; pastHistory?: string | null; additionalNotes?: string | null
}
export interface IntakeSubmissionDetail {
  id: number; patientId: number; status: IntakeStatus; submittedAt: string
  answers: IntakeAnswers; current: IntakeAnswers | null
  consent: { consentVersion: string; consentAgreed: boolean; signatureName: string; signedAt: string }
  decidedAt?: string | null; rejectionReason?: string | null
}

// ── Online booking ────────────────────────────────────────────────────────────
export type WeekDay = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'
export interface AvailabilityWindowInput { dayOfWeek: WeekDay; startTime: string; endTime: string }
export interface AvailabilityWindowDto extends AvailabilityWindowInput { id: number }
export interface BlockedDateDto { id: number; date: string; label?: string }
export interface AvailabilityDto { windows: AvailabilityWindowDto[]; blockedDates: BlockedDateDto[] }
export interface SetWeeklyAvailabilityRequest { windows: AvailabilityWindowInput[] }
export interface CreateBlockedDateRequest { date: string; label?: string }
export interface BookingSlotDto { startsAt: string; durationMinutes: number }
export interface BookAppointmentRequest { startsAt: string; reason?: string }

// ── Audit log ─────────────────────────────────────────────────────────────────
export type AuditAction = 'View' | 'Change'
export interface AuditEventDto {
  id: number; occurredAt: string; actorUserId: string; actorName: string; actorRole: string
  action: AuditAction; itemKind: string; itemId: number | null; changedFields: string[]
}
export interface AuditLogQuery {
  action?: AuditAction; actor?: string; from?: string; to?: string; page?: number; pageSize?: number
}

// ── Claim export (DRAFT CMS-1500 data worksheet; not a CMS-1500 form, not an X12 837 file) ──
export type ClaimExportFormat = 'json' | 'csv'
