import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { dashboardApi, patientsApi, appointmentsApi, prescriptionsApi, invoicesApi, vitalsApi, notesApi, attachmentsApi, portalApi, clinicalApi, labsApi, intakeApi, noteTemplatesApi, availabilityApi, waitlistApi, auditApi, reportsApi, clinicApi, staffApi, twoFactorApi } from '@/api/services'
import type { ReportQuery, QueryParams, CreatePatientRequest, UpdatePatientRequest, CreateAppointmentRequest, AppointmentStatus, CreatePrescriptionRequest, CreateInvoiceRequest, CreateVitalSignRequest, CreateMedicalNoteRequest, SaveAllergyRequest, SaveProblemRequest, SaveMedicationRequest, SaveLabOrderRequest, SaveLabResultRequest, IntakeStatus, CreateNoteTemplateRequest, AuditLogQuery, ClaimExportFormat, ClinicRole, InviteStaffRequest, TwoFactorConfirmRequest } from '@/types'
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
  clinical: (pid: number) => ['clinical', pid],
  labs: (pid: number) => ['labs', pid],
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

// ── Reports ───────────────────────────────────────────────────────────────────
export const useRevenueReport = (q: ReportQuery, enabled = true) =>
  useQuery({ queryKey: ['reports', 'revenue', q], queryFn: () => reportsApi.revenue(q), enabled })
export const useVisitsReport = (q: ReportQuery, enabled = true) =>
  useQuery({ queryKey: ['reports', 'visits', q], queryFn: () => reportsApi.visits(q), enabled })
export const useNoShowReport = (q: ReportQuery, enabled = true) =>
  useQuery({ queryKey: ['reports', 'no-shows', q], queryFn: () => reportsApi.noShows(q), enabled })
export const useArAgingReport = (page: number, enabled = true) =>
  useQuery({ queryKey: ['reports', 'ar-aging', page], queryFn: () => reportsApi.arAging(page, 20), enabled })

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

// Opens the prescription PDF in a new tab so it can be printed and signed by hand; downloads it if popups are blocked.
export const usePrintPrescription = () =>
  useMutation({
    mutationFn: async (id: number) => {
      const blob = await prescriptionsApi.downloadPdf(id)
      const url = window.URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }))
      if (!window.open(url, '_blank')) {
        const link = document.createElement('a')
        link.href = url
        link.download = `prescription-${id}.pdf`
        document.body.appendChild(link)
        link.click()
        link.remove()
      }
      window.setTimeout(() => window.URL.revokeObjectURL(url), 60_000)
    },
    onError: () => toast.error(i18n.t('toasts.prescriptionPdfFailed')),
  })

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

// Downloads a DRAFT claim data worksheet (JSON or CSV); it is not a CMS-1500 form or an X12 837 file.
export const useExportClaimDraft = () =>
  useMutation({
    mutationFn: async ({ id, format }: { id: number; format: ClaimExportFormat }) => {
      const blob = await invoicesApi.downloadClaimDraft(id, format)
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `claim-draft-${id}.${format}`
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.setTimeout(() => window.URL.revokeObjectURL(url), 60_000)
    },
    onSuccess: () => toast(i18n.t('billing.claim.draftNotice'), { icon: 'ℹ️', duration: 7000 }),
    onError: () => toast.error(i18n.t('toasts.claimExportFailed')),
  })

// ── Vitals ────────────────────────────────────────────────────────────────────
export const usePatientVitals = (patientId: number, allowed = true) =>
  useQuery({ queryKey: QK.vitals(patientId), queryFn: () => vitalsApi.getByPatient(patientId), enabled: patientId > 0 && allowed })

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

// ── Lab orders and results ────────────────────────────────────────────────────
export const usePatientLabs = (patientId: number, allowed = true) =>
  useQuery({ queryKey: QK.labs(patientId), queryFn: () => labsApi.getAll(patientId), enabled: patientId > 0 && allowed })

// eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here
const labError = (err: any) => {
  const d = err?.response?.data
  const firstValidation = d?.errors ? (Object.values(d.errors).flat() as string[])[0] : undefined
  toast.error(d?.message || firstValidation || i18n.t('labs.saveFailed'))
}

/** One hook for every lab order/result change; refreshes the patient's labs on success. */
function useLabMutation<V>(fn: (v: V) => Promise<unknown>, patientId: number, messageKey: 'labs.saved' | 'labs.removed') {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => { qc.invalidateQueries({ queryKey: QK.labs(patientId) }); toast.success(i18n.t(messageKey)) },
    onError: labError,
  })
}

export const useSaveLabOrder = (patientId: number) =>
  useLabMutation(({ id, data }: { id?: number; data: SaveLabOrderRequest }) =>
    id ? labsApi.updateOrder(patientId, id, data) : labsApi.createOrder(patientId, data), patientId, 'labs.saved')
export const useCancelLabOrder = (patientId: number) =>
  useLabMutation((id: number) => labsApi.cancelOrder(patientId, id), patientId, 'labs.saved')
export const useDeleteLabOrder = (patientId: number) =>
  useLabMutation((id: number) => labsApi.deleteOrder(patientId, id), patientId, 'labs.removed')
export const useSaveLabResult = (patientId: number, orderId: number) =>
  useLabMutation(({ id, data }: { id?: number; data: SaveLabResultRequest }) =>
    id ? labsApi.updateResult(patientId, orderId, id, data) : labsApi.addResult(patientId, orderId, data), patientId, 'labs.saved')
export const useDeleteLabResult = (patientId: number, orderId: number) =>
  useLabMutation((id: number) => labsApi.deleteResult(patientId, orderId, id), patientId, 'labs.removed')

// ── Clinical lists ────────────────────────────────────────────────────────────
export const usePatientClinical = (patientId: number) =>
  useQuery({ queryKey: QK.clinical(patientId), queryFn: () => clinicalApi.getSummary(patientId), enabled: patientId > 0 })

// eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here; narrowing would change call signatures
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
      toast.success(i18n.t('toasts.fileUploaded'))
    },
    // eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here; narrowing would change call signatures
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
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here; narrowing would change call signatures
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

// ── Waitlist ──────────────────────────────────────────────────────────────────
export const useWaitlist = (q?: QueryParams) =>
  useQuery({ queryKey: ['waitlist', q], queryFn: () => waitlistApi.getAll(q) })

export const useAddWaitlistEntry = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: waitlistApi.add,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['waitlist'] }); toast.success(i18n.t('waitlist.added')) },
    onError: (e) => toast.error(errMsg(e, i18n.t('waitlist.addFailed'))),
  })
}

export const useRemoveWaitlistEntry = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: waitlistApi.remove,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['waitlist'] }); toast.success(i18n.t('waitlist.removed')) },
    onError: (e) => toast.error(errMsg(e, i18n.t('waitlist.removeFailed'))),
  })
}

export const usePortalWaitlist = () =>
  useQuery({ queryKey: QK.portal('waitlist'), queryFn: portalApi.waitlist })

export const useJoinPortalWaitlist = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: portalApi.joinWaitlist,
    onSuccess: () => { qc.invalidateQueries({ queryKey: QK.portal('waitlist') }); toast.success(i18n.t('waitlist.joined')) },
    onError: (e) => toast.error(errMsg(e, i18n.t('waitlist.joinFailed'))),
  })
}

export const useLeavePortalWaitlist = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: portalApi.leaveWaitlist,
    onSuccess: () => { qc.invalidateQueries({ queryKey: QK.portal('waitlist') }); toast.success(i18n.t('waitlist.left')) },
    onError: (e) => toast.error(errMsg(e, i18n.t('waitlist.leaveFailed'))),
  })
}

// ── Clinic and staff ──────────────────────────────────────────────────────────
export const useClinic = (enabled = true) =>
  useQuery({ queryKey: ['clinic'], queryFn: clinicApi.get, enabled, staleTime: 60_000, retry: false })

export const useClinicDoctors = (enabled = true) =>
  useQuery({ queryKey: ['clinic', 'doctors'], queryFn: clinicApi.doctors, enabled, staleTime: 60_000 })

export const useRenameClinic = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => clinicApi.rename(name),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['clinic'] }); toast.success(i18n.t('clinic.renamed')) },
    // eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here
    onError: (err: any) => toast.error(err?.response?.data?.errors?.[0] ?? i18n.t('errors.generic')),
  })
}

export const useStaff = () =>
  useQuery({ queryKey: ['staff', 'members'], queryFn: () => staffApi.list({ page: 1, pageSize: 100 }) })

export const useStaffInvitations = () =>
  useQuery({ queryKey: ['staff', 'invitations'], queryFn: () => staffApi.invitations({ page: 1, pageSize: 100 }) })

// eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here
const staffError = (err: any) => {
  const data = err?.response?.data
  toast.error(data?.error ?? data?.errors?.[0] ?? i18n.t('errors.generic'))
}

export const useInviteStaff = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: InviteStaffRequest) => staffApi.invite(data),
    onSuccess: (r) => { qc.invalidateQueries({ queryKey: ['staff', 'invitations'] }); toast.success(r.message) },
    onError: staffError,
  })
}

export const useRevokeStaffInvitation = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => staffApi.revokeInvitation(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['staff', 'invitations'] }); toast.success(i18n.t('staff.invitationRevoked')) },
    onError: staffError,
  })
}

export const useChangeStaffRole = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, role }: { id: number; role: ClinicRole }) => staffApi.changeRole(id, role),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['staff'] }); qc.invalidateQueries({ queryKey: ['clinic'] }); toast.success(i18n.t('staff.roleChanged')) },
    onError: staffError,
  })
}

export const useSetStaffActive = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, active }: { id: number; active: boolean }) => active ? staffApi.reactivate(id) : staffApi.deactivate(id),
    onSuccess: (_d, v) => { qc.invalidateQueries({ queryKey: ['staff'] }); toast.success(i18n.t(v.active ? 'staff.reactivated' : 'staff.deactivated')) },
    onError: staffError,
  })
}

// ── Two-factor authentication (own account) ───────────────────────────────────
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here
const twoFactorError = (err: any) => toast.error(err?.response?.data?.error ?? i18n.t('errors.generic'))

export const useTwoFactorStatus = () =>
  useQuery({ queryKey: ['twoFactor'], queryFn: twoFactorApi.status })

export const useTwoFactorSetup = () =>
  useMutation({ mutationFn: twoFactorApi.setup, onError: twoFactorError })

export const useEnableTwoFactor = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (code: string) => twoFactorApi.enable(code),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['twoFactor'] }),
    onError: twoFactorError,
  })
}

export const useRegenerateRecoveryCodes = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: TwoFactorConfirmRequest) => twoFactorApi.regenerateRecoveryCodes(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['twoFactor'] }),
    onError: twoFactorError,
  })
}

export const useDisableTwoFactor = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: TwoFactorConfirmRequest) => twoFactorApi.disable(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['twoFactor'] }); toast.success(i18n.t('security.disabledToast')) },
    onError: twoFactorError,
  })
}
