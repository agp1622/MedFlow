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
  PatientAttachmentDto,
  AcceptInvitationRequest, InvitationResult,
  PortalProfileDto, PortalAppointmentDto, PortalPrescriptionDto, PortalInvoiceDto,
  PortalAttachmentDto, PortalNoteDto,
  MessageDto, MessageThreadSummaryDto, UnreadCountDto,
  DashboardStatsDto, AppointmentStatus
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
}

// ── Prescriptions ─────────────────────────────────────────────────────────────
export const prescriptionsApi = {
  getAll:       (q?: QueryParams)   => api.get<PagedResult<PrescriptionDto>>('/prescriptions', { params: q }).then(r => r.data),
  getByPatient: (patientId: number) => api.get<PrescriptionDto[]>(`/prescriptions/patient/${patientId}`).then(r => r.data),
  create:       (data: CreatePrescriptionRequest) => api.post<PrescriptionDto>('/prescriptions', data).then(r => r.data),
  update:       (id: number, data: UpdatePrescriptionRequest) => api.put(`/prescriptions/${id}`, data),
  delete:       (id: number) => api.delete(`/prescriptions/${id}`),
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
  delete:       (id: number)        => api.delete(`/medicalnotes/${id}`),
  setSharing:   (id: number, shared: boolean) => api.put<MedicalNoteDto>(`/medicalnotes/${id}/sharing`, { shared }).then(r => r.data),
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

// ── Secure messaging helpers ──────────────────────────────────────────────────
const messageForm = (body: string, files: File[]) => {
  const form = new FormData()
  form.append('body', body)
  files.forEach(f => form.append('files', f))
  return form
}
const multipart = { headers: { 'Content-Type': 'multipart/form-data' } }

// Header-authenticated blob download: no token ever goes into a URL
const downloadBlob = async (url: string, fileName: string) => {
  const response = await api.get(url, { responseType: 'blob' })
  const objectUrl = window.URL.createObjectURL(new Blob([response.data]))
  const link = document.createElement('a')
  link.href = objectUrl
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.URL.revokeObjectURL(objectUrl)
}

// ── Messaging (doctor) ────────────────────────────────────────────────────────
export const messagesApi = {
  threads:     () => api.get<MessageThreadSummaryDto[]>('/messages/threads').then(r => r.data),
  thread:      (patientId: number) => api.get<MessageDto[]>(`/messages/patient/${patientId}`).then(r => r.data),
  send:        (patientId: number, body: string, files: File[] = []) =>
    api.post<MessageDto>(`/messages/patient/${patientId}`, messageForm(body, files), multipart).then(r => r.data),
  markRead:    (patientId: number) => api.post(`/messages/patient/${patientId}/read`),
  unreadCount: () => api.get<UnreadCountDto>('/messages/unread-count').then(r => r.data),
  downloadAttachment: (id: number, fileName: string) =>
    downloadBlob(`/messages/attachments/${id}/download`, fileName),
}

// ── Patient portal (patient role only) ────────────────────────────────────────
export const portalApi = {
  me:            () => api.get<PortalProfileDto>('/portal/me').then(r => r.data),
  appointments:  () => api.get<PortalAppointmentDto[]>('/portal/appointments').then(r => r.data),
  prescriptions: () => api.get<PortalPrescriptionDto[]>('/portal/prescriptions').then(r => r.data),
  invoices:      () => api.get<PortalInvoiceDto[]>('/portal/invoices').then(r => r.data),
  attachments:   () => api.get<PortalAttachmentDto[]>('/portal/attachments').then(r => r.data),
  notes:         () => api.get<PortalNoteDto[]>('/portal/notes').then(r => r.data),
  messages:      () => api.get<MessageDto[]>('/portal/messages').then(r => r.data),
  sendMessage:   (body: string, files: File[] = []) =>
    api.post<MessageDto>('/portal/messages', messageForm(body, files), multipart).then(r => r.data),
  markMessagesRead:     () => api.post('/portal/messages/read'),
  messagesUnreadCount:  () => api.get<UnreadCountDto>('/portal/messages/unread-count').then(r => r.data),
  downloadMessageAttachment: (id: number, fileName: string) =>
    downloadBlob(`/portal/messages/attachments/${id}/download`, fileName),
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
