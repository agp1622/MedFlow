import api from './client'
import type {
  AuthResponse, LoginRequest, GoogleLoginRequest, RegisterRequest,
  PagedResult, QueryParams,
  PatientDto, PatientSummaryDto, CreatePatientRequest, UpdatePatientRequest,
  AppointmentDto, CreateAppointmentRequest, UpdateAppointmentRequest,
  PrescriptionDto, CreatePrescriptionRequest, UpdatePrescriptionRequest,
  InvoiceDto, CreateInvoiceRequest, UpdateInvoiceRequest,
  VitalSignDto, CreateVitalSignRequest,
  MedicalNoteDto, CreateMedicalNoteRequest,
  PatientAttachmentDto,
  DashboardStatsDto, AppointmentStatus
} from '@/types'

// ── Auth ──────────────────────────────────────────────────────────────────────
export const authApi = {
  login:    (data: LoginRequest)    => api.post<AuthResponse>('/auth/login', data).then(r => r.data),
  googleLogin: (data: GoogleLoginRequest) => api.post<AuthResponse>('/auth/google-login', data).then(r => r.data),
  register: (data: RegisterRequest) => api.post<AuthResponse>('/auth/register', data).then(r => r.data),
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
}
