import api from './client'
import type {
  AuthResponse, LoginRequest, GoogleLoginRequest, RegisterRequest,
  ForgotPasswordRequest, ResetPasswordRequest,
  PagedResult, QueryParams,
  PatientDto, PatientSummaryDto, CreatePatientRequest, UpdatePatientRequest,
  AppointmentDto, CreateAppointmentRequest, UpdateAppointmentRequest,
  PrescriptionDto, CreatePrescriptionRequest, UpdatePrescriptionRequest,
  InvoiceDto, CreateInvoiceRequest, UpdateInvoiceRequest,
  VitalSignDto, CreateVitalSignRequest,
  MedicalNoteDto, CreateMedicalNoteRequest,
  NoteTemplateDto, CreateNoteTemplateRequest, UpdateNoteTemplateRequest, CopyForwardDto,
  PatientAttachmentDto,
  ClinicalSummaryDto, AllergyDto, ProblemDto, MedicationDto,
  SaveAllergyRequest, SaveProblemRequest, SaveMedicationRequest,
  LabSummaryDto, LabOrderDto, SaveLabOrderRequest, SaveLabResultRequest,
  AcceptInvitationRequest, InvitationResult,
  PortalProfileDto, PortalAppointmentDto, PortalPrescriptionDto, PortalInvoiceDto,
  PortalAttachmentDto, PortalNoteDto,
  IntakeFormInfo, IntakeSubmitRequest, IntakeSubmissionSummary, IntakeSubmissionDetail, IntakeStatus,
  AvailabilityDto, AvailabilityWindowDto, SetWeeklyAvailabilityRequest, BlockedDateDto, CreateBlockedDateRequest,
  BookingSlotDto, BookAppointmentRequest,
  ReminderLookupDto, ReminderLogDto, ReminderAction,
  DashboardStatsDto, AppointmentStatus,
  AuditEventDto, AuditLogQuery
} from '@/types'

// ── Auth ──────────────────────────────────────────────────────────────────────
export const authApi = {
  login:    (data: LoginRequest)    => api.post<AuthResponse>('/auth/login', data).then(r => r.data),
  googleLogin: (data: GoogleLoginRequest) => api.post<AuthResponse>('/auth/google-login', data).then(r => r.data),
  register: (data: RegisterRequest) => api.post<AuthResponse>('/auth/register', data).then(r => r.data),
  forgotPassword: (data: ForgotPasswordRequest) => api.post<{ message: string }>('/auth/forgot-password', data).then(r => r.data),
  resetPassword:  (data: ResetPasswordRequest)  => api.post<{ message: string }>('/auth/reset-password', data).then(r => r.data),
  acceptInvitation: (data: AcceptInvitationRequest) => api.post<AuthResponse>('/auth/accept-invitation', data).then(r => r.data),
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
export const dashboardApi = {
  getStats: () => api.get<DashboardStatsDto>('/dashboard').then(r => r.data),
}

// ── Patients ──────────────────────────────────────────────────────────────────
export const patientsApi = {
  getAll:   (q?: QueryParams) => api.get<PagedResult<PatientSummaryDto>>('/patients', { params: q }).then(r => r.data),
  getById:  (id: number)      => api.get<PatientDto>(`/patients/${id}`).then(r => r.data),
  create:   (data: CreatePatientRequest)         => api.post<PatientDto>('/patients', data).then(r => r.data),
  update:   (id: number, data: UpdatePatientRequest) => api.put<PatientDto>(`/patients/${id}`, data).then(r => r.data),
  delete:   (id: number)      => api.delete(`/patients/${id}`),
  invite:   (id: number)      => api.post<InvitationResult>(`/patients/${id}/portal-invitation`).then(r => r.data),
  revokePortalAccess: (id: number) => api.delete(`/patients/${id}/portal-access`),
}

// ── Audit log ─────────────────────────────────────────────────────────────────
export const auditApi = {
  getByPatient: (patientId: number, q?: AuditLogQuery) =>
    api.get<PagedResult<AuditEventDto>>(`/patients/${patientId}/audit-log`, { params: q }).then(r => r.data),
}

// ── Appointments ──────────────────────────────────────────────────────────────
export const appointmentsApi = {
  getAll:       (q?: QueryParams) => api.get<PagedResult<AppointmentDto>>('/appointments', { params: q }).then(r => r.data),
  getToday:     ()                => api.get<AppointmentDto[]>('/appointments/today').then(r => r.data),
  getUpcoming:  (count = 5)       => api.get<AppointmentDto[]>(`/appointments/upcoming?count=${count}`).then(r => r.data),
  getByPatient: (patientId: number) => api.get<AppointmentDto[]>(`/appointments/patient/${patientId}`).then(r => r.data),
  create:       (data: CreateAppointmentRequest) => api.post<AppointmentDto>('/appointments', data).then(r => r.data),
  update:       (id: number, data: UpdateAppointmentRequest) => api.put(`/appointments/${id}`, data),
  updateStatus: (id: number, status: AppointmentStatus) => api.patch(`/appointments/${id}/status`, status, { headers: { 'Content-Type': 'application/json' } }),
  delete:       (id: number) => api.delete(`/appointments/${id}`),
  getReminders: (id: number) => api.get<ReminderLogDto>(`/appointments/${id}/reminders`).then(r => r.data),
}

// ── Appointment response (public, token from the reminder email) ──────────────
export const appointmentResponseApi = {
  lookup:  (token: string) => api.post<ReminderLookupDto>('/appointment-response/lookup', { token }).then(r => r.data),
  respond: (token: string, action: ReminderAction) =>
    api.post<ReminderLookupDto>('/appointment-response/respond', { token, action }).then(r => r.data),
}

// ── Prescriptions ─────────────────────────────────────────────────────────────
export const prescriptionsApi = {
  getAll:       (q?: QueryParams)   => api.get<PagedResult<PrescriptionDto>>('/prescriptions', { params: q }).then(r => r.data),
  getByPatient: (patientId: number) => api.get<PrescriptionDto[]>(`/prescriptions/patient/${patientId}`).then(r => r.data),
  create:       (data: CreatePrescriptionRequest) => api.post<PrescriptionDto>('/prescriptions', data).then(r => r.data),
  update:       (id: number, data: UpdatePrescriptionRequest) => api.put(`/prescriptions/${id}`, data),
  delete:       (id: number) => api.delete(`/prescriptions/${id}`),
  // Header-authenticated blob download: no token ever goes into a URL
  downloadPdf:  (id: number) => api.get<Blob>(`/prescriptions/${id}/pdf`, { responseType: 'blob' }).then(r => r.data),
}

// ── Invoices ──────────────────────────────────────────────────────────────────
export const invoicesApi = {
  getAll:       (q?: QueryParams)   => api.get<PagedResult<InvoiceDto>>('/invoices', { params: q }).then(r => r.data),
  getByPatient: (patientId: number) => api.get<InvoiceDto[]>(`/invoices/patient/${patientId}`).then(r => r.data),
  create:       (data: CreateInvoiceRequest)       => api.post<InvoiceDto>('/invoices', data).then(r => r.data),
  update:       (id: number, data: UpdateInvoiceRequest) => api.put(`/invoices/${id}`, data),
  markPaid:     (id: number)        => api.patch(`/invoices/${id}/mark-paid`),
  delete:       (id: number)        => api.delete(`/invoices/${id}`),
}

// ── Vitals ────────────────────────────────────────────────────────────────────
export const vitalsApi = {
  getByPatient: (patientId: number) => api.get<VitalSignDto[]>(`/vitalsigns/patient/${patientId}`).then(r => r.data),
  getLatest:    (patientId: number) => api.get<VitalSignDto>(`/vitalsigns/patient/${patientId}/latest`).then(r => r.data),
  create:       (data: CreateVitalSignRequest) => api.post<VitalSignDto>('/vitalsigns', data).then(r => r.data),
}

// ── Medical Notes ─────────────────────────────────────────────────────────────
export const notesApi = {
  getByPatient: (patientId: number) => api.get<MedicalNoteDto[]>(`/medicalnotes/patient/${patientId}`).then(r => r.data),
  create:       (data: CreateMedicalNoteRequest) => api.post<MedicalNoteDto>('/medicalnotes', data).then(r => r.data),
  getLatest:    (patientId: number) => api.get<CopyForwardDto>(`/medicalnotes/patient/${patientId}/latest`).then(r => r.data),
  delete:       (id: number)        => api.delete(`/medicalnotes/${id}`),
  setSharing:   (id: number, shared: boolean) => api.put<MedicalNoteDto>(`/medicalnotes/${id}/sharing`, { shared }).then(r => r.data),
}

// ── Lab orders and results ────────────────────────────────────────────────────
const labs = (patientId: number) => `/patients/${patientId}/labs`
export const labsApi = {
  getAll:       (patientId: number) => api.get<LabSummaryDto>(labs(patientId)).then(r => r.data),
  createOrder:  (patientId: number, data: SaveLabOrderRequest) => api.post<LabOrderDto>(labs(patientId), data).then(r => r.data),
  updateOrder:  (patientId: number, id: number, data: SaveLabOrderRequest) => api.put<LabOrderDto>(`${labs(patientId)}/${id}`, data).then(r => r.data),
  cancelOrder:  (patientId: number, id: number) => api.post<LabOrderDto>(`${labs(patientId)}/${id}/cancel`).then(r => r.data),
  deleteOrder:  (patientId: number, id: number) => api.delete(`${labs(patientId)}/${id}`),
  addResult:    (patientId: number, orderId: number, data: SaveLabResultRequest) => api.post<LabOrderDto>(`${labs(patientId)}/${orderId}/results`, data).then(r => r.data),
  updateResult: (patientId: number, orderId: number, id: number, data: SaveLabResultRequest) => api.put<LabOrderDto>(`${labs(patientId)}/${orderId}/results/${id}`, data).then(r => r.data),
  deleteResult: (patientId: number, orderId: number, id: number) => api.delete<LabOrderDto>(`${labs(patientId)}/${orderId}/results/${id}`).then(r => r.data),
}

// ── Clinical lists ────────────────────────────────────────────────────────────
const clinical = (patientId: number) => `/patients/${patientId}/clinical`
export const clinicalApi = {
  getSummary:       (patientId: number) => api.get<ClinicalSummaryDto>(clinical(patientId)).then(r => r.data),
  addAllergy:       (patientId: number, data: SaveAllergyRequest) => api.post<AllergyDto>(`${clinical(patientId)}/allergies`, data).then(r => r.data),
  updateAllergy:    (patientId: number, id: number, data: SaveAllergyRequest) => api.put<AllergyDto>(`${clinical(patientId)}/allergies/${id}`, data).then(r => r.data),
  deleteAllergy:    (patientId: number, id: number) => api.delete(`${clinical(patientId)}/allergies/${id}`),
  addProblem:       (patientId: number, data: SaveProblemRequest) => api.post<ProblemDto>(`${clinical(patientId)}/problems`, data).then(r => r.data),
  updateProblem:    (patientId: number, id: number, data: SaveProblemRequest) => api.put<ProblemDto>(`${clinical(patientId)}/problems/${id}`, data).then(r => r.data),
  deleteProblem:    (patientId: number, id: number) => api.delete(`${clinical(patientId)}/problems/${id}`),
  addMedication:    (patientId: number, data: SaveMedicationRequest) => api.post<MedicationDto>(`${clinical(patientId)}/medications`, data).then(r => r.data),
  updateMedication: (patientId: number, id: number, data: SaveMedicationRequest) => api.put<MedicationDto>(`${clinical(patientId)}/medications/${id}`, data).then(r => r.data),
  deleteMedication: (patientId: number, id: number) => api.delete(`${clinical(patientId)}/medications/${id}`),
}

// ── Note Templates ────────────────────────────────────────────────────────────
export const noteTemplatesApi = {
  getAll:    (params?: QueryParams) => api.get<PagedResult<NoteTemplateDto>>('/notetemplates', { params }).then(r => r.data),
  getBuiltIn: () => api.get<NoteTemplateDto[]>('/notetemplates/builtin').then(r => r.data),
  create:    (data: CreateNoteTemplateRequest) => api.post<NoteTemplateDto>('/notetemplates', data).then(r => r.data),
  update:    (id: number, data: UpdateNoteTemplateRequest) => api.put<NoteTemplateDto>(`/notetemplates/${id}`, data).then(r => r.data),
  delete:    (id: number) => api.delete(`/notetemplates/${id}`),
}

// ── Attachments ───────────────────────────────────────────────────────────────
export const attachmentsApi = {
  getByPatient: (patientId: number) =>
    api.get<PatientAttachmentDto[]>(`/attachments/patient/${patientId}`).then(r => r.data),

  upload: (data: { file: File; patientId: number; category?: string; description?: string },
           onProgress?: (pct: number) => void) => {
    const formData = new FormData()
    formData.append('file', data.file)
    formData.append('patientId', data.patientId.toString())
    if (data.category) formData.append('category', data.category)
    if (data.description) formData.append('description', data.description)
    return api.post<PatientAttachmentDto>('/attachments', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (e) => {
        if (onProgress && e.total) onProgress(Math.round((e.loaded * 100) / e.total))
      },
    }).then(r => r.data)
  },

  getPreviewUrl: (id: number) => {
    const token = localStorage.getItem('medflow_token')
    return `/api/attachments/${id}/preview?access_token=${token}`
  },
  getDownloadUrl: (id: number) => `/api/attachments/${id}/download`,

  download: async (id: number, fileName: string) => {
    const response = await api.get(`/attachments/${id}/download`, { responseType: 'blob' })
    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  },

  delete: (id: number) => api.delete(`/attachments/${id}`),
  setSharing: (id: number, shared: boolean) =>
    api.put<PatientAttachmentDto>(`/attachments/${id}/sharing`, { shared }).then(r => r.data),
}

// ── Patient portal (patient role only) ────────────────────────────────────────
export const portalApi = {
  me:            () => api.get<PortalProfileDto>('/portal/me').then(r => r.data),
  appointments:  () => api.get<PortalAppointmentDto[]>('/portal/appointments').then(r => r.data),
  prescriptions: () => api.get<PortalPrescriptionDto[]>('/portal/prescriptions').then(r => r.data),
  invoices:      () => api.get<PortalInvoiceDto[]>('/portal/invoices').then(r => r.data),
  attachments:   () => api.get<PortalAttachmentDto[]>('/portal/attachments').then(r => r.data),
  notes:         () => api.get<PortalNoteDto[]>('/portal/notes').then(r => r.data),
  bookingSlots:  (from: string, to: string) =>
    api.get<BookingSlotDto[]>('/portal/booking/slots', { params: { from, to } }).then(r => r.data),
  book:          (data: BookAppointmentRequest) =>
    api.post<PortalAppointmentDto>('/portal/booking', data).then(r => r.data),
  // Header-authenticated blob download: no token ever goes into a URL
  downloadAttachment: async (id: number, fileName: string) => {
    const response = await api.get(`/portal/attachments/${id}/download`, { responseType: 'blob' })
    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  },
}

// ── Intake forms ──────────────────────────────────────────────────────────────
export const intakeApi = {
  // Public (token-gated) form used by patients
  getForm:   (token: string) => api.get<IntakeFormInfo>(`/intake/${encodeURIComponent(token)}`).then(r => r.data),
  submit:    (token: string, data: IntakeSubmitRequest) =>
    api.post<{ message: string }>(`/intake/${encodeURIComponent(token)}`, data).then(r => r.data),
  // Doctor
  sendLink:  (patientId: number) => api.post<InvitationResult>(`/patients/${patientId}/intake-link`).then(r => r.data),
  list:      (params?: { status?: IntakeStatus; page?: number; pageSize?: number }) =>
    api.get<PagedResult<IntakeSubmissionSummary>>('/intake-submissions', { params }).then(r => r.data),
  getById:   (id: number) => api.get<IntakeSubmissionDetail>(`/intake-submissions/${id}`).then(r => r.data),
  accept:    (id: number) => api.post(`/intake-submissions/${id}/accept`),
  reject:    (id: number, reason?: string) => api.post(`/intake-submissions/${id}/reject`, { reason }),
}

// ── Availability (doctor) ─────────────────────────────────────────────────────
export const availabilityApi = {
  get:             () => api.get<AvailabilityDto>('/availability').then(r => r.data),
  setWeekly:       (data: SetWeeklyAvailabilityRequest) =>
    api.put<AvailabilityWindowDto[]>('/availability/weekly', data).then(r => r.data),
  addBlockedDate:  (data: CreateBlockedDateRequest) =>
    api.post<BlockedDateDto>('/availability/blocked-dates', data).then(r => r.data),
  removeBlockedDate: (id: number) => api.delete(`/availability/blocked-dates/${id}`),
}
