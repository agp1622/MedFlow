import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { dashboardApi, patientsApi, appointmentsApi, prescriptionsApi, invoicesApi, vitalsApi, notesApi, attachmentsApi, portalApi, clinicalApi } from '@/api/services'
import type { QueryParams, CreatePatientRequest, UpdatePatientRequest, CreateAppointmentRequest, AppointmentStatus, CreatePrescriptionRequest, CreateInvoiceRequest, CreateVitalSignRequest, CreateMedicalNoteRequest, SaveAllergyRequest, SaveProblemRequest, SaveMedicationRequest } from '@/types'
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
  clinical: (pid: number) => ['clinical', pid],
  attachments: (pid: number) => ['attachments', pid],
  portal: (section: string) => ['portal', section],
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
    mutationFn: ({ id }: { id: number; patientId: number }) => notesApi.delete(id),
    onSuccess: (_, vars) => { qc.invalidateQueries({ queryKey: QK.notes(vars.patientId) }); toast.success('Note deleted') },
  })
}

// ── Clinical lists ────────────────────────────────────────────────────────────
export const usePatientClinical = (patientId: number) =>
  useQuery({ queryKey: QK.clinical(patientId), queryFn: () => clinicalApi.getSummary(patientId), enabled: patientId > 0 })

const clinicalError = (err: any) => {
  const d = err?.response?.data
  const firstValidation = d?.errors ? (Object.values(d.errors).flat() as string[])[0] : undefined
  toast.error(typeof d === 'string' && d ? d : firstValidation || d?.title || 'Could not save changes')
}

/** One hook for add/update/remove of a clinical list; invalidates the summary on success. */
function useClinicalMutation<V>(fn: (v: V) => Promise<unknown>, patientId: number, message: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => { qc.invalidateQueries({ queryKey: QK.clinical(patientId) }); toast.success(message) },
    onError: clinicalError,
  })
}

export const useSaveAllergy = (patientId: number) =>
  useClinicalMutation(({ id, data }: { id?: number; data: SaveAllergyRequest }) =>
    id ? clinicalApi.updateAllergy(patientId, id, data) : clinicalApi.addAllergy(patientId, data), patientId, 'Allergy saved')
export const useDeleteAllergy = (patientId: number) =>
  useClinicalMutation((id: number) => clinicalApi.deleteAllergy(patientId, id), patientId, 'Allergy removed')
export const useSaveProblem = (patientId: number) =>
  useClinicalMutation(({ id, data }: { id?: number; data: SaveProblemRequest }) =>
    id ? clinicalApi.updateProblem(patientId, id, data) : clinicalApi.addProblem(patientId, data), patientId, 'Problem saved')
export const useDeleteProblem = (patientId: number) =>
  useClinicalMutation((id: number) => clinicalApi.deleteProblem(patientId, id), patientId, 'Problem removed')
export const useSaveMedication = (patientId: number) =>
  useClinicalMutation(({ id, data }: { id?: number; data: SaveMedicationRequest }) =>
    id ? clinicalApi.updateMedication(patientId, id, data) : clinicalApi.addMedication(patientId, data), patientId, 'Medication saved')
export const useDeleteMedication = (patientId: number) =>
  useClinicalMutation((id: number) => clinicalApi.deleteMedication(patientId, id), patientId, 'Medication removed')

// ── Attachments ───────────────────────────────────────────────────────────────
export const usePatientAttachments = (patientId: number) =>
  useQuery({ queryKey: QK.attachments(patientId), queryFn: () => attachmentsApi.getByPatient(patientId), enabled: patientId > 0 })

export const useUploadAttachment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: { file: File; patientId: number; category?: string; description?: string }) =>
      attachmentsApi.upload(data),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: QK.attachments(vars.patientId) })
      toast.success('File uploaded')
    },
    onError: (err: any) => {
      const d = err?.response?.data
      toast.error(typeof d === 'string' && d ? d : d?.message || d?.title || 'Failed to upload file')
    },
  })
}

export const useDeleteAttachment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id }: { id: number; patientId: number }) => attachmentsApi.delete(id),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: QK.attachments(vars.patientId) })
      toast.success('Attachment deleted')
    },
    onError: () => toast.error('Failed to delete attachment'),
  })
}

// ── Attachment / note sharing (doctor) ────────────────────────────────────────
export const useSetAttachmentSharing = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, shared }: { id: number; patientId: number; shared: boolean }) =>
      attachmentsApi.setSharing(id, shared),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: QK.attachments(vars.patientId) })
      toast.success(vars.shared ? 'Shared with patient' : 'No longer shared')
    },
    onError: () => toast.error('Failed to update sharing'),
  })
}

export const useSetNoteSharing = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, shared }: { id: number; patientId: number; shared: boolean }) =>
      notesApi.setSharing(id, shared),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: QK.notes(vars.patientId) })
      toast.success(vars.shared ? 'Shared with patient' : 'No longer shared')
    },
    onError: () => toast.error('Failed to update sharing'),
  })
}

// ── Portal invitations (doctor) ───────────────────────────────────────────────
const apiError = (err: any, fallback: string) => {
  const d = err?.response?.data
  return d?.errors?.[0] || d?.error || fallback
}

export const useInvitePatient = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (patientId: number) => patientsApi.invite(patientId),
    onSuccess: (_, patientId) => {
      qc.invalidateQueries({ queryKey: QK.patient(patientId) })
      toast.success('Invitation sent')
    },
    onError: (err) => toast.error(apiError(err, 'Failed to send invitation')),
  })
}

export const useRevokePortalAccess = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (patientId: number) => patientsApi.revokePortalAccess(patientId),
    onSuccess: (_, patientId) => {
      qc.invalidateQueries({ queryKey: QK.patient(patientId) })
      toast.success('Portal access revoked')
    },
    onError: () => toast.error('Failed to revoke access'),
  })
}

// ── Patient portal (patient) ──────────────────────────────────────────────────
export const usePortalMe = () => useQuery({ queryKey: QK.portal('me'), queryFn: portalApi.me })
export const usePortalAppointments = () => useQuery({ queryKey: QK.portal('appointments'), queryFn: portalApi.appointments })
export const usePortalPrescriptions = () => useQuery({ queryKey: QK.portal('prescriptions'), queryFn: portalApi.prescriptions })
export const usePortalInvoices = () => useQuery({ queryKey: QK.portal('invoices'), queryFn: portalApi.invoices })
export const usePortalAttachments = () => useQuery({ queryKey: QK.portal('attachments'), queryFn: portalApi.attachments })
export const usePortalNotes = () => useQuery({ queryKey: QK.portal('notes'), queryFn: portalApi.notes })
