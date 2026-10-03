import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import type { ParseKeys } from 'i18next'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Plus, Trash2 } from 'lucide-react'
import { useAvailability, useSetWeeklyAvailability, useAddBlockedDate, useRemoveBlockedDate } from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { PageSpinner, Spinner } from '@/components/ui'
import { fmt, displayEnum } from '@/utils/format'
import type { WeekDay } from '@/types'

const DAYS: WeekDay[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']
const time = z.string().regex(/^([01]\d|2[0-3]):(00|30)$/, 'availability.timeFormat')

const weeklySchema = z.object({
  windows: z.array(z.object({
    dayOfWeek: z.enum(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']),
    startTime: time,
    endTime: time,
  }).refine(w => w.endTime > w.startTime, { message: 'availability.endAfterStart', path: ['endTime'] })).max(50),
})
type WeeklyForm = z.infer<typeof weeklySchema>

const blockedSchema = z.object({
  date: z.string().min(1, 'validation.required'),
  label: z.string().max(200).optional(),
})
type BlockedForm = z.infer<typeof blockedSchema>

export function AvailabilityPage() {
  const { t } = useTranslation()
  const { data, isLoading } = useAvailability()
  const save = useSetWeeklyAvailability()
  const addBlocked = useAddBlockedDate()
  const removeBlocked = useRemoveBlockedDate()

  const weekly = useForm<WeeklyForm>({ resolver: zodResolver(weeklySchema), defaultValues: { windows: [] } })
  const { fields, append, remove } = useFieldArray({ control: weekly.control, name: 'windows' })
  const blocked = useForm<BlockedForm>({ resolver: zodResolver(blockedSchema) })

  useEffect(() => {
    if (data) weekly.reset({ windows: data.windows.map(w => ({ dayOfWeek: w.dayOfWeek, startTime: w.startTime, endTime: w.endTime })) })
  }, [data]) // eslint-disable-line react-hooks/exhaustive-deps

  if (isLoading) return <PageSpinner />

  return (
    <>
      <PageHeader title={t('availability.title')} subtitle={t('availability.subtitle')} />
      <div className="flex-1 overflow-y-auto p-8 space-y-6">
        <section className="card p-5">
          <h2 className="text-base font-semibold text-gray-900 mb-1">{t('availability.weeklyHours')}</h2>
          <p className="text-sm text-gray-500 mb-4">{t('availability.weeklyHelp')}</p>
          <form onSubmit={weekly.handleSubmit(v => save.mutate(v))} className="space-y-3">
            {fields.length === 0 && <p className="text-sm text-gray-500">{t('availability.noHours')}</p>}
            {fields.map((f, i) => (
              <div key={f.id} className="flex flex-wrap items-start gap-3">
                <select className="input w-40" {...weekly.register(`windows.${i}.dayOfWeek`)}>
                  {DAYS.map(d => <option key={d} value={d}>{displayEnum(d)}</option>)}
                </select>
                <div>
                  <input className="input w-28" type="time" step={1800} {...weekly.register(`windows.${i}.startTime`)} />
                  {weekly.formState.errors.windows?.[i]?.startTime && <p className="text-red-500 text-xs mt-1">{t(weekly.formState.errors.windows[i]?.startTime?.message as ParseKeys)}</p>}
                </div>
                <span className="pt-2 text-gray-500">{t('availability.to')}</span>
                <div>
                  <input className="input w-28" type="time" step={1800} {...weekly.register(`windows.${i}.endTime`)} />
                  {weekly.formState.errors.windows?.[i]?.endTime && <p className="text-red-500 text-xs mt-1">{t(weekly.formState.errors.windows[i]?.endTime?.message as ParseKeys)}</p>}
                </div>
                <button type="button" className="btn-secondary" aria-label={t('availability.removeWindow')} onClick={() => remove(i)}>
                  <Trash2 size={15} />
                </button>
              </div>
            ))}
            <div className="flex gap-3 pt-2">
              <button type="button" className="btn-secondary inline-flex items-center gap-1.5"
                onClick={() => append({ dayOfWeek: 'Monday', startTime: '09:00', endTime: '17:00' })}>
                <Plus size={15} /> {t('availability.addWindow')}
              </button>
              <button type="submit" className="btn-primary" disabled={save.isPending}>
                {save.isPending ? <Spinner className="w-4 h-4" /> : t('availability.saveWeekly')}
              </button>
            </div>
          </form>
        </section>

        <section className="card p-5">
          <h2 className="text-base font-semibold text-gray-900 mb-4">{t('availability.blockedDates')}</h2>
          <form onSubmit={blocked.handleSubmit(v => addBlocked.mutate({ date: v.date, label: v.label || undefined }, { onSuccess: () => blocked.reset() }))}
            className="flex flex-wrap items-start gap-3 mb-4">
            <div>
              <input className="input" type="date" {...blocked.register('date')} />
              {blocked.formState.errors.date && <p className="text-red-500 text-xs mt-1">{t(blocked.formState.errors.date.message as ParseKeys)}</p>}
            </div>
            <input className="input w-64" placeholder={t('availability.labelOptional')} maxLength={200} {...blocked.register('label')} />
            <button type="submit" className="btn-primary" disabled={addBlocked.isPending}>{t('availability.blockDate')}</button>
          </form>
          {data?.blockedDates.length ? (
            <ul className="divide-y divide-gray-100">
              {data.blockedDates.map(b => (
                <li key={b.id} className="py-2 flex items-center justify-between">
                  <span className="text-sm text-gray-900">{fmt.date(b.date)}{b.label ? ` · ${b.label}` : ''}</span>
                  <button className="btn-secondary" aria-label={t('availability.unblockDate')} onClick={() => removeBlocked.mutate(b.id)}>
                    <Trash2 size={15} />
                  </button>
                </li>
              ))}
            </ul>
          ) : <p className="text-sm text-gray-500">{t('availability.noBlocked')}</p>}
        </section>
      </div>
    </>
  )
}
