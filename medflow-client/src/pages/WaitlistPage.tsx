import { useTranslation } from 'react-i18next'
import type { ParseKeys } from 'i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Trash2 } from 'lucide-react'
import { useWaitlist, usePatients, useAddWaitlistEntry, useRemoveWaitlistEntry } from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { EmptyState, PageSpinner, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'

const addSchema = z.object({
  patientId: z.coerce.number().min(1, 'appointments.selectPatient'),
})
type AddForm = z.infer<typeof addSchema>

export function WaitlistPage() {
  const { t } = useTranslation()
  const { data, isLoading } = useWaitlist({ pageSize: 100 })
  const { data: patients } = usePatients({ pageSize: 200 })
  const add = useAddWaitlistEntry()
  const remove = useRemoveWaitlistEntry()
  const { register, handleSubmit, reset, formState: { errors } } = useForm<AddForm>({ resolver: zodResolver(addSchema) })

  if (isLoading) return <PageSpinner />

  const waitingIds = new Set(data?.items.map(e => e.patientId))

  return (
    <>
      <PageHeader title={t('waitlist.title')} subtitle={t('waitlist.subtitle')} />
      <div className="flex-1 overflow-y-auto p-8 space-y-6">
        <section className="card p-5">
          <h2 className="text-base font-semibold text-gray-900 mb-4">{t('waitlist.addTitle')}</h2>
          <form onSubmit={handleSubmit(v => add.mutate(v.patientId, { onSuccess: () => reset() }))}
            className="flex flex-wrap items-start gap-3">
            <div className="min-w-[220px] flex-1">
              <label className="label" htmlFor="waitlist-patient">{t('waitlist.patient')}</label>
              <select id="waitlist-patient" className="input" {...register('patientId')}>
                <option value="">{t('waitlist.selectPatient')}</option>
                {patients?.items.filter(p => !waitingIds.has(p.id))
                  .map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
              </select>
              {errors.patientId && <p className="text-red-500 text-xs mt-1">{t(errors.patientId.message as ParseKeys)}</p>}
            </div>
            <button type="submit" className="btn-primary mt-6" disabled={add.isPending}>
              {add.isPending ? <Spinner className="w-4 h-4" /> : t('waitlist.add')}
            </button>
          </form>
        </section>

        <section className="card p-5">
          {!data || data.items.length === 0 ? (
            <EmptyState title={t('waitlist.empty')} />
          ) : (
            <ul className="divide-y divide-gray-100">
              {data.items.map((e, i) => (
                <li key={e.id} className="py-3 flex flex-wrap items-center justify-between gap-2">
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-gray-900">{i + 1}. {e.patientName}</p>
                    <p className="text-xs text-gray-500">{t('waitlist.joinedAt')} {fmt.date(e.joinedAt)}</p>
                  </div>
                  <button className="btn-secondary" disabled={remove.isPending}
                    aria-label={`${t('waitlist.remove')} ${e.patientName}`}
                    onClick={() => remove.mutate(e.id)}>
                    <Trash2 size={14} /> {t('waitlist.remove')}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </>
  )
}
