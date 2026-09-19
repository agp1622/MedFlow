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
}

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
  lastVisit?: string; nextAppointment?: string
  createdAt: string; updatedAt: string
}
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
}
export interface CreateMedicalNoteRequest {
  patientId: number; content: string; visitType?: string
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
