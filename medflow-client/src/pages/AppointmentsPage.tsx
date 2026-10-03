import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ParseKeys } from 'i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import {
  useAppointments, useAppointmentReminders, useCreateAppointment, useUpdateAppointmentStatus, useDeleteAppointment, usePatients
} from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { Avatar, Badge, PageSpinner, EmptyState, SearchInput, Pagination, Spinner } from '@/components/ui'
import { Modal } from './PatientsPage'
import { fmt, displayEnum } from '@/utils/format'
import { Trash2, CheckCircle, XCircle, Bell } from 'lucide-react'
import type { CreateAppointmentRequest } from '@/types'

export function AppointmentsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const [remindersFor, setRemindersFor] = useState<number | null>(null)
  const { data, isLoading } = useAppointments({ page, pageSize: 20, search })
  const updateStatus = useUpdateAppointmentStatus()
  const deleteAppt = useDeleteAppointment()

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={t('appointments.title')} subtitle={t('appointments.total', { count: data?.totalCount ?? 0 })}
        action={{ label: t('appointments.new'), onClick: () => setShowModal(true) }}>
        <SearchInput value={search} onChange={v => { setSearch(v); setPage(1) }} placeholder={t('patients.search')} />
      </PageHeader>

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6">
          <div className="card overflow-x-auto">
            <table className="w-full min-w-[640px]">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {(['patient', 'date', 'time', 'type', 'duration', 'reason', 'status', 'actions'] as const).map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide whitespace-nowrap">{t(`appointments.cols.${h}`)}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.length === 0 && (
                  <tr><td colSpan={8}><EmptyState title={t('appointments.empty')} description={t('appointments.emptyHint')} /></td></tr>
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
                    <td className="px-4 py-3 text-sm text-gray-500">{t('common.minutes', { count: a.durationMinutes })}</td>
                    <td className="px-4 py-3 text-sm text-gray-500 max-w-36 truncate">{a.reason ?? '—'}</td>
                    <td className="px-4 py-3"><Badge status={a.status} /></td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        {a.status === 'Pending' && (
                          <button className="btn-secondary p-1.5" title={t('appointments.confirm')}
                            onClick={() => updateStatus.mutate({ id: a.id, status: 'Confirmed' })}>
                            <CheckCircle size={14} />
                          </button>
                        )}
                        {(a.status === 'Pending' || a.status === 'Confirmed') && (
                          <button className="btn-danger p-1.5" title={t('common.cancel')}
                            onClick={() => updateStatus.mutate({ id: a.id, status: 'Cancelled' })}>
                            <XCircle size={14} />
                          </button>
                        )}
                        <button className="btn-ghost p-1.5" title={t('appointments.reminderLog')}
                          onClick={() => setRemindersFor(a.id)}>
                          <Bell size={14} className="text-gray-400" />
                        </button>
                        <button className="btn-ghost p-1.5" title={t('common.delete')}
                          onClick={() => { if (confirm(t('appointments.confirmDelete'))) deleteAppt.mutate(a.id) }}>
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
      {remindersFor !== null && <ReminderLogModal id={remindersFor} onClose={() => setRemindersFor(null)} />}
      {showModal && <NewAppointmentModal onClose={() => setShowModal(false)} />}
    </div>
  )
}

// ── Reminder log ──────────────────────────────────────────────────────────────
function ReminderLogModal({ id, onClose }: { id: number; onClose: () => void }) {
  const { t } = useTranslation()
  const { data, isLoading } = useAppointmentReminders(id)
  return (
    <Modal title={t('appointments.reminderLog')} onClose={onClose}>
      {isLoading || !data ? <PageSpinner /> : (
        <div className="space-y-4">
          <p className="text-sm text-gray-700">
            {t('appointments.patientResponse')} <strong>{data.response === 'None' ? t('appointments.noResponse') : displayEnum(data.response)}</strong>
            {data.respondedAt && <span className="text-gray-500"> ({fmt.date(data.respondedAt)} {fmt.time(data.respondedAt)})</span>}
          </p>
          {data.deliveries.length === 0 ? (
            <p className="text-sm text-gray-500">{t('appointments.noReminders')}</p>
          ) : (
            <ul className="divide-y divide-border">
              {data.deliveries.map((d, i) => (
                <li key={i} className="py-2 text-sm flex justify-between gap-4">
                  <span>{displayEnum(d.channel)}: <strong>{displayEnum(d.outcome)}</strong>{d.reason ? ` - ${d.reason}` : ''}</span>
                  <span className="text-gray-500 whitespace-nowrap">{fmt.date(d.attemptedAt)} {fmt.time(d.attemptedAt)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Modal>
  )
}

// ── New Appointment Modal ─────────────────────────────────────────────────────
const apptSchema = z.object({
  patientId: z.coerce.number().min(1, 'appointments.selectPatient'),
  scheduledAt: z.string().min(1, 'validation.required'),
  durationMinutes: z.coerce.number().min(5).max(480),
  type: z.enum(['NewPatient','FollowUp','CheckUp','Consultation','LabReview','Emergency']),
  reason: z.string().optional(),
  location: z.string().optional(),
})
type ApptForm = z.infer<typeof apptSchema>

function NewAppointmentModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
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
    <Modal title={t('appointments.schedule')} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label className="label">{t('appointments.form.patient')}</label>
          <select className="input" {...register('patientId')}>
            <option value="">{t('appointments.form.selectPatient')}</option>
            {patients?.items.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
          </select>
          {errors.patientId && <p className="text-red-500 text-xs mt-1">{t(errors.patientId.message as ParseKeys)}</p>}
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="label">{t('appointments.form.dateTime')}</label>
            <input className="input" type="datetime-local" {...register('scheduledAt')} />
            {errors.scheduledAt && <p className="text-red-500 text-xs mt-1">{t(errors.scheduledAt.message as ParseKeys)}</p>}
          </div>
          <div>
            <label className="label">{t('appointments.form.duration')}</label>
            <input className="input" type="number" min={5} step={5} {...register('durationMinutes')} />
          </div>
        </div>
        <div>
          <label className="label">{t('appointments.form.type')}</label>
          <select className="input" {...register('type')}>
            {['NewPatient','FollowUp','CheckUp','Consultation','LabReview','Emergency'].map(k => (
              <option key={k} value={k}>{displayEnum(k)}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="label">{t('appointments.form.reason')}</label>
          <input className="input" placeholder={t('appointments.form.reasonPlaceholder')} {...register('reason')} />
        </div>
        <div>
          <label className="label">{t('appointments.form.location')}</label>
          <input className="input" placeholder={t('appointments.form.locationPlaceholder')} {...register('location')} />
        </div>
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="btn-primary" disabled={create.isPending}>
            {create.isPending ? <Spinner className="w-4 h-4" /> : t('appointments.form.submit')}
          </button>
        </div>
      </form>
    </Modal>
  )
}
