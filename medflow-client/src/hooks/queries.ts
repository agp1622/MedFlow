import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { dashboardApi, patientsApi, appointmentsApi, prescriptionsApi, invoicesApi, vitalsApi, notesApi, attachmentsApi, portalApi, intakeApi, noteTemplatesApi, availabilityApi, auditApi } from '@/api/services'
import type { QueryParams, CreatePatientRequest, UpdatePatientRequest, CreateAppointmentRequest, AppointmentStatus, CreatePrescriptionRequest, CreateInvoiceRequest, CreateVitalSignRequest, CreateMedicalNoteRequest, IntakeStatus, CreateNoteTemplateRequest, AuditLogQuery } from '@/types'
import toast from 'react-hot-toast'
import i18n from '@/i18n'

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
  noteTemplates: ['noteTemplates'],
  attachments: (pid: number) => ['attachments', pid],
  auditLog: (pid: number, q?: AuditLogQuery) => ['audit-log', pid, q],
  portal: (section: string) => ['portal', section],
  intakeList: (status?: IntakeStatus, page?: number) => ['intake', 'list', status, page],
  intake: (id: number) => ['intake', id],
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
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['patients'] }); toast.success(i18n.t('toasts.patientCreated')) },
    onError: () => toast.error(i18n.t('toasts.patientCreateFailed')),
  })
}

export const useUpdatePatient = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: UpdatePatientRequest) => patientsApi.update(id, data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['patients'] }); toast.success(i18n.t('toasts.patientUpdated')) },
    onError: () => toast.error(i18n.t('toasts.patientUpdateFailed')),
  })
}

export const useDeletePatient = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => patientsApi.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['patients'] }); toast.success(i18n.t('toasts.patientRemoved')) },
    onError: () => toast.error(i18n.t('toasts.patientRemoveFailed')),
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

export const useAppointmentReminders = (id: number) =>
  useQuery({ queryKey: ['appointments', 'reminders', id], queryFn: () => appointmentsApi.getReminders(id) })

export const useCreateAppointment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateAppointmentRequest) => appointmentsApi.create(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['appointments'] }); toast.success(i18n.t('toasts.appointmentScheduled')) },
    onError: () => toast.error(i18n.t('toasts.appointmentScheduleFailed')),
  })
}

export const useUpdateAppointmentStatus = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: AppointmentStatus }) => appointmentsApi.updateStatus(id, status),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['appointments'] }); toast.success(i18n.t('toasts.statusUpdated')) },
  })
}

export const useDeleteAppointment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => appointmentsApi.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['appointments'] }); toast.success(i18n.t('toasts.appointmentCancelled')) },
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
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['prescriptions'] }); toast.success(i18n.t('toasts.prescriptionCreated')) },
    onError: () => toast.error(i18n.t('toasts.prescriptionCreateFailed')),
  })
}

export const useDeletePrescription = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => prescriptionsApi.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['prescriptions'] }); toast.success(i18n.t('toasts.prescriptionRemoved')) },
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
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['invoices'] }); toast.success(i18n.t('toasts.invoiceCreated')) },
    onError: () => toast.error(i18n.t('toasts.invoiceCreateFailed')),
  })
}

export const useMarkInvoicePaid = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => invoicesApi.markPaid(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['invoices'] }); toast.success(i18n.t('toasts.invoicePaid')) },
  })
}

// ── Vitals ────────────────────────────────────────────────────────────────────
export const usePatientVitals = (patientId: number) =>
  useQuery({ queryKey: QK.vitals(patientId), queryFn: () => vitalsApi.getByPatient(patientId), enabled: patientId > 0 })

export const useCreateVital = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateVitalSignRequest) => vitalsApi.create(data),
    onSuccess: (_, vars) => { qc.invalidateQueries({ queryKey: QK.vitals(vars.patientId) }); toast.success(i18n.t('toasts.vitalsRecorded')) },
  })
}

// ── Notes ─────────────────────────────────────────────────────────────────────
export const usePatientNotes = (patientId: number) =>
  useQuery({ queryKey: QK.notes(patientId), queryFn: () => notesApi.getByPatient(patientId), enabled: patientId > 0 })

export const useNoteTemplates = () =>
  useQuery({
    queryKey: QK.noteTemplates,
    queryFn: async () => {
      const [builtIn, own] = await Promise.all([noteTemplatesApi.getBuiltIn(), noteTemplatesApi.getAll({ page: 1, pageSize: 100 })])
      return [...builtIn, ...own.items]
    },
  })

export const useSaveNoteTemplate = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id?: number; data: CreateNoteTemplateRequest }) =>
      id ? noteTemplatesApi.update(id, data) : noteTemplatesApi.create(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: QK.noteTemplates }); toast.success(i18n.t('toasts.templateSaved')) },
  })
}

export const useDeleteNoteTemplate = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => noteTemplatesApi.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: QK.noteTemplates }); toast.success(i18n.t('toasts.templateDeleted')) },
  })
}

export const useCopyForward = () =>
  useMutation({ mutationFn: (patientId: number) => notesApi.getLatest(patientId) })

export const useCreateNote = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateMedicalNoteRequest) => notesApi.create(data),
    onSuccess: (_, vars) => { qc.invalidateQueries({ queryKey: QK.notes(vars.patientId) }); toast.success(i18n.t('toasts.noteSaved')) },
  })
}

export const useDeleteNote = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id }: { id: number; patientId: number }) => notesApi.delete(id),
    onSuccess: (_, vars) => { qc.invalidateQueries({ queryKey: QK.notes(vars.patientId) }); toast.success(i18n.t('toasts.noteDeleted')) },
  })
}

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
      toast.success(i18n.t('toasts.fileUploaded'))
    },
    onError: (err: any) => {
      const d = err?.response?.data
      toast.error(typeof d === 'string' && d ? d : d?.message || d?.title || i18n.t('toasts.fileUploadFailed'))
    },
  })
}

export const useDeleteAttachment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id }: { id: number; patientId: number }) => attachmentsApi.delete(id),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: QK.attachments(vars.patientId) })
      toast.success(i18n.t('toasts.attachmentDeleted'))
    },
    onError: () => toast.error(i18n.t('toasts.attachmentDeleteFailed')),
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
      toast.success(vars.shared ? i18n.t('toasts.shared') : i18n.t('toasts.unshared'))
    },
    onError: () => toast.error(i18n.t('toasts.sharingFailed')),
  })
}

export const useSetNoteSharing = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, shared }: { id: number; patientId: number; shared: boolean }) =>
      notesApi.setSharing(id, shared),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: QK.notes(vars.patientId) })
      toast.success(vars.shared ? i18n.t('toasts.shared') : i18n.t('toasts.unshared'))
    },
    onError: () => toast.error(i18n.t('toasts.sharingFailed')),
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
      toast.success(i18n.t('toasts.invitationSent'))
    },
    onError: (err) => toast.error(apiError(err, i18n.t('toasts.invitationFailed'))),
  })
}

export const useRevokePortalAccess = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (patientId: number) => patientsApi.revokePortalAccess(patientId),
    onSuccess: (_, patientId) => {
      qc.invalidateQueries({ queryKey: QK.patient(patientId) })
      toast.success(i18n.t('toasts.portalRevoked'))
    },
    onError: () => toast.error(i18n.t('toasts.revokeFailed')),
  })
}

// ── Patient portal (patient) ──────────────────────────────────────────────────
export const usePortalMe = () => useQuery({ queryKey: QK.portal('me'), queryFn: portalApi.me })
export const usePortalAppointments = () => useQuery({ queryKey: QK.portal('appointments'), queryFn: portalApi.appointments })
export const usePortalPrescriptions = () => useQuery({ queryKey: QK.portal('prescriptions'), queryFn: portalApi.prescriptions })
export const usePortalInvoices = () => useQuery({ queryKey: QK.portal('invoices'), queryFn: portalApi.invoices })
export const usePortalAttachments = () => useQuery({ queryKey: QK.portal('attachments'), queryFn: portalApi.attachments })
export const usePortalNotes = () => useQuery({ queryKey: QK.portal('notes'), queryFn: portalApi.notes })

// ── Intake forms (doctor) ─────────────────────────────────────────────────────
export const useSendIntakeLink = () =>
  useMutation({
    mutationFn: (patientId: number) => intakeApi.sendLink(patientId),
    onSuccess: () => toast.success(i18n.t('toasts.intakeSent')),
    onError: (err) => toast.error(apiError(err, i18n.t('toasts.intakeSendFailed'))),
  })

export const useIntakeSubmissions = (status?: IntakeStatus, page = 1) =>
  useQuery({ queryKey: QK.intakeList(status, page), queryFn: () => intakeApi.list({ status, page, pageSize: 20 }) })

export const useIntakeSubmission = (id: number) =>
  useQuery({ queryKey: QK.intake(id), queryFn: () => intakeApi.getById(id), enabled: id > 0 })

export const useDecideIntake = (id: number) => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ accept, reason }: { accept: boolean; reason?: string }) =>
      accept ? intakeApi.accept(id) : intakeApi.reject(id, reason),
    onSuccess: (_, v) => {
      qc.invalidateQueries({ queryKey: ['intake'] })
      qc.invalidateQueries({ queryKey: ['patients'] })
      toast.success(v.accept ? i18n.t('toasts.intakeAccepted') : i18n.t('toasts.intakeRejected'))
    },
    onError: (err) => toast.error(apiError(err, i18n.t('toasts.decisionFailed'))),
  })
}

// ── Availability (doctor) ─────────────────────────────────────────────────────
export const useAvailability = () => useQuery({ queryKey: ['availability'], queryFn: availabilityApi.get })

const errMsg = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? fallback

export const useSetWeeklyAvailability = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: availabilityApi.setWeekly,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['availability'] }); toast.success(i18n.t('toasts.availabilitySaved')) },
    onError: (e) => toast.error(errMsg(e, i18n.t('toasts.availabilityFailed'))),
  })
}

export const useAddBlockedDate = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: availabilityApi.addBlockedDate,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['availability'] }); toast.success(i18n.t('toasts.dateBlocked')) },
    onError: (e) => toast.error(errMsg(e, i18n.t('toasts.blockFailed'))),
  })
}

export const useRemoveBlockedDate = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: availabilityApi.removeBlockedDate,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['availability'] }) },
  })
}

// ── Online booking (patient) ──────────────────────────────────────────────────
export const usePortalSlots = (date: string) =>
  useQuery({ queryKey: [...QK.portal('slots'), date], queryFn: () => portalApi.bookingSlots(date, date), enabled: !!date })

export const useBookAppointment = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: portalApi.book,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QK.portal('appointments') })
      qc.invalidateQueries({ queryKey: QK.portal('slots') })
      toast.success(i18n.t('toasts.appointmentBooked'))
    },
    onError: (e) => {
      qc.invalidateQueries({ queryKey: QK.portal('slots') })
      toast.error(errMsg(e, i18n.t('toasts.bookFailed')))
    },
  })
}

// ── Audit log ─────────────────────────────────────────────────────────────────
export const useAuditLog = (patientId: number, q?: AuditLogQuery) =>
  useQuery({ queryKey: QK.auditLog(patientId, q), queryFn: () => auditApi.getByPatient(patientId, q), enabled: patientId > 0 })
