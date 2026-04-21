import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { dashboardApi, patientsApi, appointmentsApi, prescriptionsApi, invoicesApi, vitalsApi, notesApi } from '@/api/services'
import type { QueryParams, CreatePatientRequest, UpdatePatientRequest, CreateAppointmentRequest, UpdateAppointmentRequest, AppointmentStatus, CreatePrescriptionRequest, UpdatePrescriptionRequest, CreateInvoiceRequest, UpdateInvoiceRequest, CreateVitalSignRequest, CreateMedicalNoteRequest } from '@/types'
import toast from 'react-hot-toast'

// Keys
export const QK = {
  dashboard: ['dashboard'],
  patients: (q?: QueryParams) => ['patients', q],
  patient: (id: number) => ['patients', id],
  appointments: (q?: QueryParams) => ['appointments', q],
  appointmentsToday: ['appointments', 'today'],
  appointmentsUpcoming: ['appointments', 'upcoming'],
  appointmentsByPatient: (pid: number) => ['appointments', 'patient', pid],
  prescriptions: (q?: QueryParams) => ['prescriptions', q],
  prescriptionsByPatient: (pid: number) => ['prescriptions', 'patient', pid],
  invoices: (q?: QueryParams) => ['invoices', q],
  invoicesByPatient: (pid: number) => ['invoices', 'patient', pid],
  vitals: (pid: number) => ['vitals', pid],
  notes: (pid: number) => ['notes', pid],
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
export const useDashboard = () =>
  useQuery({ queryKey: QK.dashboard, queryFn: dashboardApi.getStats, staleTime: 60_000 })

// ── Patients ──────────────────────────────────────────────────────────────────
export const usePatients = (q?: QueryParams) =>
  useQuery({ queryKey: QK.patients(q), queryFn: () => patientsApi.getAll(q) })

export const usePatient = (id: number) =>
  useQuery({ queryKey: QK.patient(id), queryFn: () => patientsApi.getById(id), enabled: id > 0 })

export const useCreatePatient = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreatePatientRequest) => patientsApi.create(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['patients'] }); toast.success('Patient created') },
    onError: () => toast.error('Failed to create patient'),
  })
}

export const useUpdatePatient = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: UpdatePatientRequest) => patientsApi.update(id, data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['patients'] }); toast.success('Patient updated') },
    onError: () => toast.error('Failed to update patient'),
  })
}

export const useDeletePatient = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => patientsApi.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['patients'] }); toast.success('Patient removed') },
    onError: () => toast.error('Failed to remove patient'),
  })
}

// ── Appointments ──────────────────────────────────────────────────────────────
export const useAppointments = (q?: QueryParams) =>
  useQuery({ queryKey: QK.appointments(q), queryFn: () => appointmentsApi.getAll(q) })

export const useTodayAppointments = () =>
  useQuery({ queryKey: QK.appointmentsToday, queryFn: appointmentsApi.getToday })

export const useUpcomingAppointments = (count = 5) =>
  useQuery({ queryKey: QK.appointmentsUpcoming, queryFn: () => appointmentsApi.getUpcoming(count) })

export const usePatientAppointments = (patientId: number) =>
  useQuery({ queryKey: QK.appointmentsByPatient(patientId), queryFn: () => appointmentsApi.getByPatient(patientId), enabled: patientId > 0 })

export const useCreateAppointment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateAppointmentRequest) => appointmentsApi.create(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['appointments'] }); toast.success('Appointment scheduled') },
    onError: () => toast.error('Failed to schedule appointment'),
  })
}

export const useUpdateAppointmentStatus = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: AppointmentStatus }) => appointmentsApi.updateStatus(id, status),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['appointments'] }); toast.success('Status updated') },
  })
}

export const useDeleteAppointment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => appointmentsApi.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['appointments'] }); toast.success('Appointment cancelled') },
  })
}

// ── Prescriptions ─────────────────────────────────────────────────────────────
export const usePrescriptions = (q?: QueryParams) =>
  useQuery({ queryKey: QK.prescriptions(q), queryFn: () => prescriptionsApi.getAll(q) })

export const usePatientPrescriptions = (patientId: number) =>
  useQuery({ queryKey: QK.prescriptionsByPatient(patientId), queryFn: () => prescriptionsApi.getByPatient(patientId), enabled: patientId > 0 })

export const useCreatePrescription = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreatePrescriptionRequest) => prescriptionsApi.create(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['prescriptions'] }); toast.success('Prescription created') },
    onError: () => toast.error('Failed to create prescription'),
  })
}

export const useDeletePrescription = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => prescriptionsApi.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['prescriptions'] }); toast.success('Prescription removed') },
  })
}

// ── Invoices ──────────────────────────────────────────────────────────────────
export const useInvoices = (q?: QueryParams) =>
  useQuery({ queryKey: QK.invoices(q), queryFn: () => invoicesApi.getAll(q) })

export const usePatientInvoices = (patientId: number) =>
  useQuery({ queryKey: QK.invoicesByPatient(patientId), queryFn: () => invoicesApi.getByPatient(patientId), enabled: patientId > 0 })

export const useCreateInvoice = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateInvoiceRequest) => invoicesApi.create(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['invoices'] }); toast.success('Invoice created') },
    onError: () => toast.error('Failed to create invoice'),
  })
}

export const useMarkInvoicePaid = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => invoicesApi.markPaid(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['invoices'] }); toast.success('Invoice marked as paid') },
  })
}

// ── Vitals ────────────────────────────────────────────────────────────────────
export const usePatientVitals = (patientId: number) =>
  useQuery({ queryKey: QK.vitals(patientId), queryFn: () => vitalsApi.getByPatient(patientId), enabled: patientId > 0 })

export const useCreateVital = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateVitalSignRequest) => vitalsApi.create(data),
    onSuccess: (_, vars) => { qc.invalidateQueries({ queryKey: QK.vitals(vars.patientId) }); toast.success('Vitals recorded') },
  })
}

// ── Notes ─────────────────────────────────────────────────────────────────────
export const usePatientNotes = (patientId: number) =>
  useQuery({ queryKey: QK.notes(patientId), queryFn: () => notesApi.getByPatient(patientId), enabled: patientId > 0 })

export const useCreateNote = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateMedicalNoteRequest) => notesApi.create(data),
    onSuccess: (_, vars) => { qc.invalidateQueries({ queryKey: QK.notes(vars.patientId) }); toast.success('Note saved') },
  })
}

export const useDeleteNote = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, patientId }: { id: number; patientId: number }) => notesApi.delete(id),
    onSuccess: (_, vars) => { qc.invalidateQueries({ queryKey: QK.notes(vars.patientId) }); toast.success('Note deleted') },
  })
}
