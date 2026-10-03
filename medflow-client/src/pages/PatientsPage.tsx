import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { tError } from '@/i18n'
import { useNavigate, useParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  usePatients, usePatient, useCreatePatient, useDeletePatient,
  usePatientAppointments, usePatientPrescriptions, usePatientInvoices,
  usePatientVitals, usePatientNotes, useCreateNote, useDeleteNote,
  useSetNoteSharing, useNoteTemplates, useCopyForward, useInvitePatient, useRevokePortalAccess, useSendIntakeLink,
} from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { Avatar, Badge, SearchInput, PageSpinner, EmptyState, Pagination, Spinner } from '@/components/ui'
import { fmt, bloodTypeDisplay, displayEnum } from '@/utils/format'
import toast from 'react-hot-toast'
import { ArrowLeft, Trash2, Plus } from 'lucide-react'
import { ShareToggle } from '@/components/sharing/ShareToggle'
import { ClinicalPanel } from '@/components/clinical/ClinicalPanel'
import { AttachmentsTab } from '@/components/attachments/AttachmentsTab'
import { AuditLogTab } from '@/components/audit/AuditLogTab'
import type { CreatePatientRequest } from '@/types'

// ── Patient List ──────────────────────────────────────────────────────────────
export function PatientsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const { data, isLoading } = usePatients({ page, pageSize: 20, search })
  const deletePatient = useDeletePatient()

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={t('patients.title')} subtitle={t('patients.total', { count: data?.totalCount ?? 0 })}
        action={{ label: t('patients.new'), onClick: () => setShowModal(true) }}>
        <SearchInput value={search} onChange={v => { setSearch(v); setPage(1) }} placeholder={t('patients.search')} />
      </PageHeader>

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6">
          <div className="card overflow-x-auto">
            <table className="w-full min-w-[640px]">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {(['patient', 'age', 'condition', 'blood', 'lastVisit', 'nextAppt', 'status', ''] as const).map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide whitespace-nowrap">{h && t(`patients.cols.${h}`)}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.length === 0 && (
                  <tr><td colSpan={8}><EmptyState title={t('patients.empty')} description={t('patients.emptyHint')} /></td></tr>
                )}
                {data?.items.map(p => (
                  <tr key={p.id} className="table-row-hover" onClick={() => navigate(`/patients/${p.id}`)}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <Avatar name={p.fullName} />
                        <div>
                          <p className="font-semibold text-gray-900">{p.fullName}</p>
                          <p className="text-xs text-gray-400">{p.email}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-700">{p.age}</td>
                    <td className="px-4 py-3 text-gray-700">{p.primaryCondition ?? '—'}</td>
                    <td className="px-4 py-3">
                      <span className="font-bold text-primary-600 text-sm">{bloodTypeDisplay[p.bloodType] ?? p.bloodType}</span>
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-sm">{fmt.date(p.lastVisit)}</td>
                    <td className="px-4 py-3 text-gray-500 text-sm">{fmt.dateShort(p.nextAppointment)}</td>
                    <td className="px-4 py-3"><Badge status={p.status} /></td>
                    <td className="px-4 py-3" onClick={e => e.stopPropagation()}>
                      <button className="btn-danger p-1.5" onClick={() => {
                        if (confirm(t('patients.confirmRemove'))) deletePatient.mutate(p.id)
                      }}><Trash2 size={14} /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={page} totalPages={data?.totalPages ?? 1} onPage={setPage} />
          </div>
        </div>
      )}
      {showModal && <NewPatientModal onClose={() => setShowModal(false)} />}
    </div>
  )
}

// ── Patient Detail ────────────────────────────────────────────────────────────
const TABS = ['overview', 'appointments', 'prescriptions', 'invoices', 'notes', 'attachments', 'audit'] as const
type Tab = typeof TABS[number]

export function PatientDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const patientId = Number(id)
  const navigate = useNavigate()
  const [tab, setTab] = useState<Tab>('overview')
  const { data: patient, isLoading } = usePatient(patientId)
  const invite = useInvitePatient()
  const revoke = useRevokePortalAccess()
  const sendIntake = useSendIntakeLink()

  if (isLoading) return <PageSpinner />
  if (!patient) return <div className="p-8 text-gray-500">{t('patients.notFound')}</div>

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={patient.fullName}
        subtitle={`${patient.primaryCondition ?? t('patients.noCondition')} · ${t('common.age', { count: patient.age })}`}>
        <button className="btn-ghost gap-1" onClick={() => navigate('/patients')}>
          <ArrowLeft size={15} /> {t('common.back')}
        </button>
      </PageHeader>

      <div className="flex-1 overflow-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6 space-y-5">
        {/* Patient header card */}
        <div className="card p-4 sm:p-5 flex flex-col sm:flex-row gap-4 sm:gap-6">
          <Avatar name={patient.fullName} size="lg" />
          <div className="flex-1 min-w-0 grid grid-cols-2 md:grid-cols-4 gap-4 break-words">
            {([
              ['fullName', patient.fullName],
              ['dateOfBirth', fmt.date(patient.dateOfBirth)],
              ['gender', displayEnum(patient.gender)],
              ['bloodType', bloodTypeDisplay[patient.bloodType] ?? patient.bloodType],
              ['email', patient.email],
              ['phone', patient.phone],
              ['insurance', patient.insuranceProvider ?? '—'],
              ['allergies', patient.allergies ?? '—'],
            ] as const).map(([k, v]) => (
              <div key={k}>
                <p className="label">{t(`patients.fields.${k}`)}</p>
                <p className="text-sm font-medium text-gray-800">{v}</p>
              </div>
            ))}
          </div>
          <div className="flex flex-col sm:items-end gap-2">
            <Badge status={patient.status} />
            <button className="btn-secondary text-xs" disabled={sendIntake.isPending || !patient.email?.trim()}
              title={patient.email?.trim() ? undefined : t('patients.emailRequired')}
              onClick={() => sendIntake.mutate(patientId)}>
              {sendIntake.isPending ? <Spinner className="w-3.5 h-3.5" /> : t('patients.sendIntake')}
            </button>
            <PortalAccess
              status={patient.portalStatus ?? 'NotInvited'}
              hasEmail={!!patient.email?.trim()}
              busy={invite.isPending || revoke.isPending}
              onInvite={() => invite.mutate(patientId)}
              onRevoke={() => { if (confirm(t('patients.confirmRevoke'))) revoke.mutate(patientId) }}
            />
          </div>
        </div>

        {/* Allergies, problems, medications: always visible */}
        <ClinicalPanel patientId={patientId} legacyAllergies={patient.allergies} />

        {/* Tabs */}
        <div className="flex gap-1 bg-white border border-border rounded-xl p-1 w-fit max-w-full overflow-x-auto">
          {TABS.map(k => (
            <button key={k} onClick={() => setTab(k)}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-all whitespace-nowrap ${
                tab === k ? 'bg-primary-600 text-white shadow-sm' : 'text-gray-500 hover:text-gray-700 hover:bg-gray-50'
              }`}>{t(`patients.tabs.${k}`)}</button>
          ))}
        </div>

        {/* Tab content */}
        {tab === 'overview'      && <OverviewTab patient={patient} patientId={patientId} />}
        {tab === 'appointments'  && <ApptTab patientId={patientId} />}
        {tab === 'prescriptions' && <RxTab patientId={patientId} />}
        {tab === 'invoices'      && <InvTab patientId={patientId} />}
        {tab === 'notes'         && <NotesTab patientId={patientId} />}
        {tab === 'attachments'   && <AttachmentsTab patientId={patientId} />}
        {tab === 'audit'         && <AuditLogTab patientId={patientId} />}
      </div>
    </div>
  )
}

function OverviewTab({ patient, patientId }: { patient: ReturnType<typeof usePatient>['data']; patientId: number }) {
  const { t } = useTranslation()
  const { data: vitals } = usePatientVitals(patientId)
  const latest = vitals?.[0]
  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
      <div className="card p-5">
        <h3 className="font-bold text-gray-800 mb-4">{t('patients.vitals.title')}</h3>
        {!latest ? <p className="text-gray-400 text-sm">{t('patients.vitals.none')}</p> : (
          <div className="space-y-2">
            {[
              ['bloodPressure', latest.bloodPressure, 'text-amber-600'],
              ['heartRate', latest.heartRate ? `${fmt.number(latest.heartRate)} bpm` : null, 'text-emerald-600'],
              ['weight', latest.weight ? `${fmt.number(latest.weight)} kg` : null, 'text-gray-700'],
              ['bmi', latest.bmi != null ? fmt.number(latest.bmi) : null, 'text-gray-700'],
              ['temperature', latest.temperature ? `${fmt.number(latest.temperature)}°C` : null, 'text-gray-700'],
              ['oxygen', latest.oxygenSaturation ? `${fmt.number(latest.oxygenSaturation)}%` : null, 'text-blue-600'],
            ].filter(([, v]) => v).map(([k, v, c]) => (
              <div key={k as string} className="flex justify-between py-1.5 border-b border-border last:border-0">
                <span className="text-sm text-gray-500">{t(`patients.vitals.${k as 'bloodPressure'}`)}</span>
                <span className={`text-sm font-bold ${c as string}`}>{v as string}</span>
              </div>
            ))}
            <p className="text-xs text-gray-400 mt-2">{t('patients.vitals.recorded', { when: fmt.relative(latest.recordedAt) })}</p>
          </div>
        )}
      </div>
      <div className="card p-5">
        <h3 className="font-bold text-gray-800 mb-4">{t('patients.medicalNotes')}</h3>
        <p className="text-sm text-gray-600 leading-relaxed">{patient?.notes ?? t('patients.noNotesOnFile')}</p>
      </div>
    </div>
  )
}

function ApptTab({ patientId }: { patientId: number }) {
  const { t } = useTranslation()
  const { data, isLoading } = usePatientAppointments(patientId)
  if (isLoading) return <PageSpinner />
  return (
    <div className="card overflow-x-auto">
      <SimpleTable
        headers={[t('patients.appt.date'), t('patients.appt.time'), t('patients.appt.type'), t('patients.appt.duration'), t('patients.appt.status'), t('patients.appt.notes')]}
        empty={!data?.length}
        rows={data?.map(a => [
          fmt.date(a.scheduledAt), fmt.time(a.scheduledAt),
          displayEnum(a.type), t('common.minutes', { count: a.durationMinutes }),
          <Badge key="s" status={a.status} />,
          a.notes ?? '—'
        ]) ?? []}
      />
    </div>
  )
}

function RxTab({ patientId }: { patientId: number }) {
  const { t } = useTranslation()
  const { data, isLoading } = usePatientPrescriptions(patientId)
  if (isLoading) return <PageSpinner />
  return (
    <div className="card overflow-x-auto">
      <SimpleTable
        headers={[t('patients.rx.drug'), t('patients.rx.dosage'), t('patients.rx.frequency'), t('patients.rx.issued'), t('patients.rx.expires'), t('patients.rx.refills'), t('patients.rx.status')]}
        empty={!data?.length}
        rows={data?.map(rx => [
          <span key="d" className="font-semibold">{rx.drugName}</span>,
          rx.dosage, rx.frequency,
          fmt.date(rx.issuedDate), fmt.date(rx.expiryDate),
          rx.refillsRemaining,
          <Badge key="s" status={rx.status} />
        ]) ?? []}
      />
    </div>
  )
}

function InvTab({ patientId }: { patientId: number }) {
  const { t } = useTranslation()
  const { data, isLoading } = usePatientInvoices(patientId)
  if (isLoading) return <PageSpinner />
  return (
    <div className="card overflow-x-auto">
      <SimpleTable
        headers={[t('patients.inv.number'), t('patients.inv.date'), t('patients.inv.service'), t('patients.inv.amount'), t('patients.inv.status')]}
        empty={!data?.length}
        rows={data?.map(inv => [
          <span key="n" className="font-semibold text-primary-600">{inv.invoiceNumber}</span>,
          fmt.date(inv.invoiceDate), inv.serviceDescription,
          <span key="a" className="font-bold">{fmt.currency(inv.amount)}</span>,
          <Badge key="s" status={inv.status} />
        ]) ?? []}
      />
    </div>
  )
}

function PortalAccess({ status, hasEmail, busy, onInvite, onRevoke }: {
  status: 'NotInvited' | 'Invited' | 'Active'; hasEmail: boolean; busy: boolean
  onInvite: () => void; onRevoke: () => void
}) {
  const { t } = useTranslation()
  const label = t(`patients.portal.${status}`)
  return (
    <div className="flex flex-col sm:items-end gap-1.5 sm:text-right">
      <span className={`text-xs font-medium ${status === 'Active' ? 'text-emerald-600' : 'text-gray-500'}`}>{label}</span>
      {status !== 'Active' && (
        <button className="btn-secondary text-xs" onClick={onInvite} disabled={busy || !hasEmail}
          title={hasEmail ? undefined : t('patients.portal.addEmailFirst')}>
          {busy ? <Spinner className="w-3.5 h-3.5" /> : status === 'Invited' ? t('patients.portal.resend') : t('patients.portal.invite')}
        </button>
      )}
      {status !== 'NotInvited' && (
        <button className="text-xs text-red-500 hover:underline disabled:opacity-50" onClick={onRevoke} disabled={busy}>
          {status === 'Active' ? t('patients.portal.revoke') : t('patients.portal.cancel')}
        </button>
      )}
      {!hasEmail && status === 'NotInvited' && <span className="text-[11px] text-gray-400">{t('patients.emailRequired')}</span>}
    </div>
  )
}

function NotesTab({ patientId }: { patientId: number }) {
  const { t } = useTranslation()
  const { data, isLoading } = usePatientNotes(patientId)
  const createNote = useCreateNote()
  const deleteNote = useDeleteNote()
  const setSharing = useSetNoteSharing()
  const [content, setContent] = useState('')
  const [visitType, setVisitType] = useState('')
  const { data: templates } = useNoteTemplates()
  const copyForward = useCopyForward()

  // Never silently discard what the doctor has already typed.
  const applyText = (text: string) => {
    if (content.trim() && !window.confirm(t('patients.notes.confirmReplace'))) return
    setContent(text)
  }
  const handlePick = (id: string) => {
    const tpl = templates?.find(x => String(x.id) === id)
    if (tpl) applyText(tpl.body)
  }
  const handleCopyForward = () =>
    copyForward.mutate(patientId, {
      onSuccess: n => { applyText(n.content); if (n.visitType && !visitType) setVisitType(n.visitType) },
      onError: () => toast.error(t('patients.notes.noPrevious')),
    })

  const handleAdd = () => {
    if (!content.trim()) return
    createNote.mutate({ patientId, content, visitType: visitType || undefined },
      { onSuccess: () => { setContent(''); setVisitType('') } })
  }

  if (isLoading) return <PageSpinner />
  return (
    <div className="space-y-4">
      <div className="card p-4 space-y-3">
        <p className="font-semibold text-gray-800 text-sm">{t('patients.notes.add')}</p>
        <input className="input" placeholder={t('patients.notes.visitType')} value={visitType} onChange={e => setVisitType(e.target.value)} />
        <div className="flex flex-wrap gap-2">
          <select className="input w-auto" value="" aria-label={t('patients.notes.insertTemplate')} onChange={e => handlePick(e.target.value)}>
            <option value="">{t('patients.notes.useTemplate')}</option>
            {templates?.map(tpl => <option key={`${tpl.isBuiltIn}-${tpl.id}`} value={tpl.id}>{tpl.name}</option>)}
          </select>
          <button type="button" className="btn-ghost" onClick={handleCopyForward} disabled={copyForward.isPending}>
            {t('patients.notes.copyForward')}
          </button>
        </div>
        <textarea className="input resize-none" rows={8} placeholder={t('patients.notes.placeholder')} value={content} onChange={e => setContent(e.target.value)} />
        <button className="btn-primary" onClick={handleAdd} disabled={createNote.isPending || !content.trim()}>
          {createNote.isPending ? <Spinner className="w-4 h-4" /> : <><Plus size={14} /> {t('patients.notes.save')}</>}
        </button>
      </div>
      {data?.length === 0 && <EmptyState title={t('patients.notes.empty')} />}
      <div className="space-y-3">
        {data?.map(note => (
          <div key={note.id} className="card p-4">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs text-gray-400">{fmt.dateTime(note.noteDate)} · {note.visitType ?? t('patients.notes.general')}</p>
                <p className="text-sm text-gray-700 mt-1 leading-relaxed">{note.content}</p>
                <div className="mt-2">
                  <ShareToggle shared={!!note.sharedWithPatient} disabled={setSharing.isPending}
                    onChange={shared => setSharing.mutate({ id: note.id, patientId, shared })} />
                </div>
              </div>
              <button className="btn-ghost p-1.5 flex-shrink-0" onClick={() => deleteNote.mutate({ id: note.id, patientId })}>
                <Trash2 size={13} className="text-gray-400" />
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

// ── SimpleTable helper ────────────────────────────────────────────────────────
function SimpleTable({ headers, rows, empty }: { headers: string[]; rows: React.ReactNode[][]; empty: boolean }) {
  const { t } = useTranslation()
  return (
    <table className="w-full min-w-[640px]">
      <thead>
        <tr className="bg-gray-50 border-b border-border">
          {headers.map(h => <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">{h}</th>)}
        </tr>
      </thead>
      <tbody className="divide-y divide-border">
        {empty ? (
          <tr><td colSpan={headers.length}><EmptyState title={t('common.nothingYet')} /></td></tr>
        ) : rows.map((row, i) => (
          <tr key={i}>
            {row.map((cell, j) => <td key={j} className="px-4 py-3 text-sm text-gray-700">{cell}</td>)}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

// ── New Patient Modal ─────────────────────────────────────────────────────────
const patientSchema = z.object({
  firstName: z.string().min(1, 'validation.required'),
  lastName: z.string().min(1, 'validation.required'),
  dateOfBirth: z.string().min(1, 'validation.required'),
  gender: z.enum(['Male', 'Female', 'NonBinary', 'PreferNotToSay']),
  bloodType: z.enum(['APos','ANeg','BPos','BNeg','ABPos','ABNeg','OPos','ONeg','Unknown']),
  email: z.string().email('validation.invalidEmail'),
  phone: z.string().min(7, 'validation.required'),
  primaryCondition: z.string().optional(),
  allergies: z.string().optional(),
  insuranceProvider: z.string().optional(),
})
type PatientForm = z.infer<typeof patientSchema>

function NewPatientModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const { register, handleSubmit, formState: { errors } } = useForm<PatientForm>({ resolver: zodResolver(patientSchema) })
  const create = useCreatePatient()

  const onSubmit = (data: PatientForm) => {
    create.mutate(data as unknown as CreatePatientRequest, { onSuccess: onClose })
  }

  return (
    <Modal title={t('patients.new')} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label={t('patients.form.firstName')} error={errors.firstName?.message}><input className="input" {...register('firstName')} /></Field>
          <Field label={t('patients.form.lastName')} error={errors.lastName?.message}><input className="input" {...register('lastName')} /></Field>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label={t('patients.form.dateOfBirth')} error={errors.dateOfBirth?.message}><input className="input" type="date" {...register('dateOfBirth')} /></Field>
          <Field label={t('patients.form.gender')} error={errors.gender?.message}>
            <select className="input" {...register('gender')}>
              <option value="">{t('common.select')}</option>
              {['Male','Female','NonBinary','PreferNotToSay'].map(g => <option key={g} value={g}>{displayEnum(g)}</option>)}
            </select>
          </Field>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label={t('patients.form.email')} error={errors.email?.message}><input className="input" type="email" {...register('email')} /></Field>
          <Field label={t('patients.form.phone')} error={errors.phone?.message}><input className="input" {...register('phone')} /></Field>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label={t('patients.form.bloodType')} error={errors.bloodType?.message}>
            <select className="input" {...register('bloodType')}>
              <option value="">{t('common.select')}</option>
              {Object.entries(bloodTypeDisplay).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </Field>
          <Field label={t('patients.form.primaryCondition')}><input className="input" {...register('primaryCondition')} /></Field>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label={t('patients.form.allergies')}><input className="input" {...register('allergies')} /></Field>
          <Field label={t('patients.form.insurance')}><input className="input" {...register('insuranceProvider')} /></Field>
        </div>
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="btn-primary" disabled={create.isPending}>
            {create.isPending ? <Spinner className="w-4 h-4" /> : t('patients.form.create')}
          </button>
        </div>
      </form>
    </Modal>
  )
}

export function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  const { t } = useTranslation()
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center" onClick={onClose}>
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
      <div className="relative bg-white rounded-2xl shadow-modal w-full max-w-lg mx-4 max-h-[90vh] max-h-[90dvh] overflow-y-auto"
        onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between px-4 sm:px-6 py-4 border-b border-border">
          <h2 className="font-bold text-gray-900">{title}</h2>
          <button className="btn-ghost p-1" aria-label={t('common.close')} onClick={onClose}>✕</button>
        </div>
        <div className="px-4 sm:px-6 py-5">{children}</div>
      </div>
    </div>
  )
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  const { t } = useTranslation()
  return (
    <div>
      <label className="label">{label}</label>
      {children}
      {error && <p className="text-red-500 text-xs mt-1">{tError(t, error)}</p>}
    </div>
  )
}
