import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  usePatients, usePatient, useCreatePatient, useDeletePatient,
  usePatientAppointments, usePatientPrescriptions, usePatientInvoices,
  usePatientVitals, usePatientNotes, useCreateNote, useDeleteNote,
} from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { Avatar, Badge, SearchInput, PageSpinner, EmptyState, Pagination, Spinner } from '@/components/ui'
import { fmt, bloodTypeDisplay, displayEnum } from '@/utils/format'
import { ArrowLeft, Trash2, Plus } from 'lucide-react'
import type { CreatePatientRequest, Gender, BloodType } from '@/types'
import toast from 'react-hot-toast'

// ── Patient List ──────────────────────────────────────────────────────────────
export function PatientsPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const { data, isLoading } = usePatients({ page, pageSize: 20, search })
  const deletePatient = useDeletePatient()

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title="Patients" subtitle={`${data?.totalCount ?? 0} total`}
        action={{ label: 'New Patient', onClick: () => setShowModal(true) }}>
        <SearchInput value={search} onChange={v => { setSearch(v); setPage(1) }} placeholder="Search patients..." />
      </PageHeader>

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-8 py-6">
          <div className="card overflow-hidden">
            <table className="w-full">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {['Patient', 'Age', 'Condition', 'Blood', 'Last Visit', 'Next Appt', 'Status', ''].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.length === 0 && (
                  <tr><td colSpan={8}><EmptyState title="No patients found" description="Add your first patient to get started" /></td></tr>
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
                        if (confirm('Remove this patient?')) deletePatient.mutate(p.id)
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
const TABS = ['Overview', 'Appointments', 'Prescriptions', 'Invoices', 'Notes'] as const
type Tab = typeof TABS[number]

export function PatientDetailPage() {
  const { id } = useParams<{ id: string }>()
  const patientId = Number(id)
  const navigate = useNavigate()
  const [tab, setTab] = useState<Tab>('Overview')
  const { data: patient, isLoading } = usePatient(patientId)

  if (isLoading) return <PageSpinner />
  if (!patient) return <div className="p-8 text-gray-500">Patient not found</div>

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={patient.fullName}
        subtitle={`${patient.primaryCondition ?? 'No condition'} · Age ${patient.age}`}>
        <button className="btn-ghost gap-1" onClick={() => navigate('/patients')}>
          <ArrowLeft size={15} /> Back
        </button>
      </PageHeader>

      <div className="flex-1 overflow-auto px-8 py-6 space-y-5">
        {/* Patient header card */}
        <div className="card p-5 flex gap-6">
          <Avatar name={patient.fullName} size="lg" />
          <div className="flex-1 grid grid-cols-4 gap-4">
            {[
              ['Full Name', patient.fullName],
              ['Date of Birth', fmt.date(patient.dateOfBirth)],
              ['Gender', patient.gender],
              ['Blood Type', bloodTypeDisplay[patient.bloodType] ?? patient.bloodType],
              ['Email', patient.email],
              ['Phone', patient.phone],
              ['Insurance', patient.insuranceProvider ?? '—'],
              ['Allergies', patient.allergies ?? '—'],
            ].map(([k, v]) => (
              <div key={k}>
                <p className="label">{k}</p>
                <p className="text-sm font-medium text-gray-800">{v}</p>
              </div>
            ))}
          </div>
          <Badge status={patient.status} />
        </div>

        {/* Tabs */}
        <div className="flex gap-1 bg-white border border-border rounded-xl p-1 w-fit">
          {TABS.map(t => (
            <button key={t} onClick={() => setTab(t)}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                tab === t ? 'bg-primary-600 text-white shadow-sm' : 'text-gray-500 hover:text-gray-700 hover:bg-gray-50'
              }`}>{t}</button>
          ))}
        </div>

        {/* Tab content */}
        {tab === 'Overview'      && <OverviewTab patient={patient} patientId={patientId} />}
        {tab === 'Appointments'  && <ApptTab patientId={patientId} />}
        {tab === 'Prescriptions' && <RxTab patientId={patientId} />}
        {tab === 'Invoices'      && <InvTab patientId={patientId} />}
        {tab === 'Notes'         && <NotesTab patientId={patientId} />}
      </div>
    </div>
  )
}

function OverviewTab({ patient, patientId }: { patient: ReturnType<typeof usePatient>['data']; patientId: number }) {
  const { data: vitals } = usePatientVitals(patientId)
  const latest = vitals?.[0]
  return (
    <div className="grid grid-cols-2 gap-5">
      <div className="card p-5">
        <h3 className="font-bold text-gray-800 mb-4">Latest Vitals</h3>
        {!latest ? <p className="text-gray-400 text-sm">No vitals recorded</p> : (
          <div className="space-y-2">
            {[
              ['Blood Pressure', latest.bloodPressure, 'text-amber-600'],
              ['Heart Rate', latest.heartRate ? `${latest.heartRate} bpm` : null, 'text-emerald-600'],
              ['Weight', latest.weight ? `${latest.weight} kg` : null, 'text-gray-700'],
              ['BMI', latest.bmi?.toString(), 'text-gray-700'],
              ['Temperature', latest.temperature ? `${latest.temperature}°C` : null, 'text-gray-700'],
              ['O₂ Saturation', latest.oxygenSaturation ? `${latest.oxygenSaturation}%` : null, 'text-blue-600'],
            ].filter(([, v]) => v).map(([k, v, c]) => (
              <div key={k as string} className="flex justify-between py-1.5 border-b border-border last:border-0">
                <span className="text-sm text-gray-500">{k as string}</span>
                <span className={`text-sm font-bold ${c as string}`}>{v as string}</span>
              </div>
            ))}
            <p className="text-xs text-gray-400 mt-2">Recorded {fmt.relative(latest.recordedAt)}</p>
          </div>
        )}
      </div>
      <div className="card p-5">
        <h3 className="font-bold text-gray-800 mb-4">Medical Notes</h3>
        <p className="text-sm text-gray-600 leading-relaxed">{patient?.notes ?? 'No notes on file.'}</p>
      </div>
    </div>
  )
}

function ApptTab({ patientId }: { patientId: number }) {
  const { data, isLoading } = usePatientAppointments(patientId)
  if (isLoading) return <PageSpinner />
  return (
    <div className="card overflow-hidden">
      <SimpleTable
        headers={['Date', 'Time', 'Type', 'Duration', 'Status', 'Notes']}
        empty={!data?.length}
        rows={data?.map(a => [
          fmt.date(a.scheduledAt), fmt.time(a.scheduledAt),
          displayEnum(a.type), `${a.durationMinutes}min`,
          <Badge key="s" status={a.status} />,
          a.notes ?? '—'
        ]) ?? []}
      />
    </div>
  )
}

function RxTab({ patientId }: { patientId: number }) {
  const { data, isLoading } = usePatientPrescriptions(patientId)
  if (isLoading) return <PageSpinner />
  return (
    <div className="card overflow-hidden">
      <SimpleTable
        headers={['Drug', 'Dosage', 'Frequency', 'Issued', 'Expires', 'Refills', 'Status']}
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
  const { data, isLoading } = usePatientInvoices(patientId)
  if (isLoading) return <PageSpinner />
  return (
    <div className="card overflow-hidden">
      <SimpleTable
        headers={['Invoice #', 'Date', 'Service', 'Amount', 'Status']}
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

function NotesTab({ patientId }: { patientId: number }) {
  const { data, isLoading } = usePatientNotes(patientId)
  const createNote = useCreateNote()
  const deleteNote = useDeleteNote()
  const [content, setContent] = useState('')
  const [visitType, setVisitType] = useState('')

  const handleAdd = () => {
    if (!content.trim()) return
    createNote.mutate({ patientId, content, visitType: visitType || undefined },
      { onSuccess: () => { setContent(''); setVisitType('') } })
  }

  if (isLoading) return <PageSpinner />
  return (
    <div className="space-y-4">
      <div className="card p-4 space-y-3">
        <p className="font-semibold text-gray-800 text-sm">Add Note</p>
        <input className="input" placeholder="Visit type (e.g. Follow-up)" value={visitType} onChange={e => setVisitType(e.target.value)} />
        <textarea className="input resize-none" rows={3} placeholder="Clinical notes..." value={content} onChange={e => setContent(e.target.value)} />
        <button className="btn-primary" onClick={handleAdd} disabled={createNote.isPending || !content.trim()}>
          {createNote.isPending ? <Spinner className="w-4 h-4" /> : <><Plus size={14} /> Save Note</>}
        </button>
      </div>
      {data?.length === 0 && <EmptyState title="No notes yet" />}
      <div className="space-y-3">
        {data?.map(note => (
          <div key={note.id} className="card p-4">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs text-gray-400">{fmt.dateTime(note.noteDate)} · {note.visitType ?? 'General'}</p>
                <p className="text-sm text-gray-700 mt-1 leading-relaxed">{note.content}</p>
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
  return (
    <table className="w-full">
      <thead>
        <tr className="bg-gray-50 border-b border-border">
          {headers.map(h => <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">{h}</th>)}
        </tr>
      </thead>
      <tbody className="divide-y divide-border">
        {empty ? (
          <tr><td colSpan={headers.length}><EmptyState title="Nothing here yet" /></td></tr>
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
  firstName: z.string().min(1, 'Required'),
  lastName: z.string().min(1, 'Required'),
  dateOfBirth: z.string().min(1, 'Required'),
  gender: z.enum(['Male', 'Female', 'NonBinary', 'PreferNotToSay']),
  bloodType: z.enum(['APos','ANeg','BPos','BNeg','ABPos','ABNeg','OPos','ONeg','Unknown']),
  email: z.string().email('Invalid email'),
  phone: z.string().min(7, 'Required'),
  primaryCondition: z.string().optional(),
  allergies: z.string().optional(),
  insuranceProvider: z.string().optional(),
})
type PatientForm = z.infer<typeof patientSchema>

function NewPatientModal({ onClose }: { onClose: () => void }) {
  const { register, handleSubmit, formState: { errors } } = useForm<PatientForm>({ resolver: zodResolver(patientSchema) })
  const create = useCreatePatient()

  const onSubmit = (data: PatientForm) => {
    create.mutate(data as unknown as CreatePatientRequest, { onSuccess: onClose })
  }

  return (
    <Modal title="New Patient" onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <Field label="First name" error={errors.firstName?.message}><input className="input" {...register('firstName')} /></Field>
          <Field label="Last name" error={errors.lastName?.message}><input className="input" {...register('lastName')} /></Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Date of birth" error={errors.dateOfBirth?.message}><input className="input" type="date" {...register('dateOfBirth')} /></Field>
          <Field label="Gender" error={errors.gender?.message}>
            <select className="input" {...register('gender')}>
              <option value="">Select...</option>
              {['Male','Female','NonBinary','PreferNotToSay'].map(g => <option key={g} value={g}>{displayEnum(g)}</option>)}
            </select>
          </Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Email" error={errors.email?.message}><input className="input" type="email" {...register('email')} /></Field>
          <Field label="Phone" error={errors.phone?.message}><input className="input" {...register('phone')} /></Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Blood type" error={errors.bloodType?.message}>
            <select className="input" {...register('bloodType')}>
              <option value="">Select...</option>
              {Object.entries(bloodTypeDisplay).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </Field>
          <Field label="Primary condition"><input className="input" {...register('primaryCondition')} /></Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Allergies"><input className="input" {...register('allergies')} /></Field>
          <Field label="Insurance provider"><input className="input" {...register('insuranceProvider')} /></Field>
        </div>
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn-primary" disabled={create.isPending}>
            {create.isPending ? <Spinner className="w-4 h-4" /> : 'Create Patient'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

export function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center" onClick={onClose}>
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
      <div className="relative bg-white rounded-2xl shadow-modal w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto"
        onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-border">
          <h2 className="font-bold text-gray-900">{title}</h2>
          <button className="btn-ghost p-1" onClick={onClose}>✕</button>
        </div>
        <div className="px-6 py-5">{children}</div>
      </div>
    </div>
  )
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="label">{label}</label>
      {children}
      {error && <p className="text-red-500 text-xs mt-1">{error}</p>}
    </div>
  )
}
