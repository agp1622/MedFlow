import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import {
  useAppointments, useCreateAppointment, useUpdateAppointmentStatus, useDeleteAppointment, usePatients
} from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { Avatar, Badge, PageSpinner, EmptyState, SearchInput, Pagination, Spinner } from '@/components/ui'
import { Modal } from './PatientsPage'
import { fmt, displayEnum } from '@/utils/format'
import { Trash2, CheckCircle, XCircle } from 'lucide-react'
import type { CreateAppointmentRequest, AppointmentStatus } from '@/types'

export function AppointmentsPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const { data, isLoading } = useAppointments({ page, pageSize: 20, search })
  const updateStatus = useUpdateAppointmentStatus()
  const deleteAppt = useDeleteAppointment()

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title="Appointments" subtitle={`${data?.totalCount ?? 0} total`}
        action={{ label: 'New Appointment', onClick: () => setShowModal(true) }}>
        <SearchInput value={search} onChange={v => { setSearch(v); setPage(1) }} placeholder="Search patients..." />
      </PageHeader>

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-8 py-6">
          <div className="card overflow-hidden">
            <table className="w-full">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {['Patient', 'Date', 'Time', 'Type', 'Duration', 'Reason', 'Status', 'Actions'].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.length === 0 && (
                  <tr><td colSpan={8}><EmptyState title="No appointments" description="Schedule your first appointment" /></td></tr>
                )}
                {data?.items.map(a => (
                  <tr key={a.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2.5 cursor-pointer" onClick={() => navigate(`/patients/${a.patientId}`)}>
                        <Avatar name={a.patientName} size="sm" />
                        <span className="font-semibold text-gray-900 text-sm">{a.patientName}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700">{fmt.date(a.scheduledAt)}</td>
                    <td className="px-4 py-3 text-sm font-bold text-primary-600">{fmt.time(a.scheduledAt)}</td>
                    <td className="px-4 py-3 text-sm text-gray-700">{displayEnum(a.type)}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{a.durationMinutes}min</td>
                    <td className="px-4 py-3 text-sm text-gray-500 max-w-36 truncate">{a.reason ?? '—'}</td>
                    <td className="px-4 py-3"><Badge status={a.status} /></td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        {a.status === 'Pending' && (
                          <button className="btn-secondary p-1.5" title="Confirm"
                            onClick={() => updateStatus.mutate({ id: a.id, status: 'Confirmed' })}>
                            <CheckCircle size={14} />
                          </button>
                        )}
                        {(a.status === 'Pending' || a.status === 'Confirmed') && (
                          <button className="btn-danger p-1.5" title="Cancel"
                            onClick={() => updateStatus.mutate({ id: a.id, status: 'Cancelled' })}>
                            <XCircle size={14} />
                          </button>
                        )}
                        <button className="btn-ghost p-1.5" title="Delete"
                          onClick={() => { if (confirm('Delete appointment?')) deleteAppt.mutate(a.id) }}>
                          <Trash2 size={14} className="text-gray-400" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={page} totalPages={data?.totalPages ?? 1} onPage={setPage} />
          </div>
        </div>
      )}
      {showModal && <NewAppointmentModal onClose={() => setShowModal(false)} />}
    </div>
  )
}

// ── New Appointment Modal ─────────────────────────────────────────────────────
const apptSchema = z.object({
  patientId: z.coerce.number().min(1, 'Select a patient'),
  scheduledAt: z.string().min(1, 'Required'),
  durationMinutes: z.coerce.number().min(5).max(480),
  type: z.enum(['NewPatient','FollowUp','CheckUp','Consultation','LabReview','Emergency']),
  reason: z.string().optional(),
  location: z.string().optional(),
})
type ApptForm = z.infer<typeof apptSchema>

function NewAppointmentModal({ onClose }: { onClose: () => void }) {
  const { register, handleSubmit, formState: { errors } } = useForm<ApptForm>({
    resolver: zodResolver(apptSchema),
    defaultValues: { durationMinutes: 30, type: 'FollowUp' }
  })
  const create = useCreateAppointment()
  const { data: patients } = usePatients({ pageSize: 200 })

  const onSubmit = (data: ApptForm) => {
    create.mutate(data as CreateAppointmentRequest, { onSuccess: onClose })
  }

  return (
    <Modal title="Schedule Appointment" onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label className="label">Patient</label>
          <select className="input" {...register('patientId')}>
            <option value="">Select patient...</option>
            {patients?.items.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
          </select>
          {errors.patientId && <p className="text-red-500 text-xs mt-1">{errors.patientId.message}</p>}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Date & Time</label>
            <input className="input" type="datetime-local" {...register('scheduledAt')} />
            {errors.scheduledAt && <p className="text-red-500 text-xs mt-1">{errors.scheduledAt.message}</p>}
          </div>
          <div>
            <label className="label">Duration (min)</label>
            <input className="input" type="number" min={5} step={5} {...register('durationMinutes')} />
          </div>
        </div>
        <div>
          <label className="label">Appointment Type</label>
          <select className="input" {...register('type')}>
            {['NewPatient','FollowUp','CheckUp','Consultation','LabReview','Emergency'].map(t => (
              <option key={t} value={t}>{displayEnum(t)}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="label">Reason</label>
          <input className="input" placeholder="Reason for visit..." {...register('reason')} />
        </div>
        <div>
          <label className="label">Location</label>
          <input className="input" placeholder="Room / clinic location" {...register('location')} />
        </div>
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn-primary" disabled={create.isPending}>
            {create.isPending ? <Spinner className="w-4 h-4" /> : 'Schedule'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
