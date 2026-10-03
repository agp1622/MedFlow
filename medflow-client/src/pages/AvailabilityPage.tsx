import { useEffect } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Plus, Trash2 } from 'lucide-react'
import { useAvailability, useSetWeeklyAvailability, useAddBlockedDate, useRemoveBlockedDate } from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { PageSpinner, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import type { WeekDay } from '@/types'

const DAYS: WeekDay[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']
const time = z.string().regex(/^([01]\d|2[0-3]):(00|30)$/, 'Use HH:00 or HH:30')

const weeklySchema = z.object({
  windows: z.array(z.object({
    dayOfWeek: z.enum(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']),
    startTime: time,
    endTime: time,
  }).refine(w => w.endTime > w.startTime, { message: 'End must be after start', path: ['endTime'] })).max(50),
})
type WeeklyForm = z.infer<typeof weeklySchema>

const blockedSchema = z.object({
  date: z.string().min(1, 'Required'),
  label: z.string().max(200).optional(),
})
type BlockedForm = z.infer<typeof blockedSchema>

export function AvailabilityPage() {
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
      <PageHeader title="Availability" subtitle="Hours patients can book online (times are UTC)" />
      <div className="flex-1 overflow-y-auto p-8 space-y-6">
        <section className="card p-5">
          <h2 className="text-base font-semibold text-gray-900 mb-1">Weekly hours</h2>
          <p className="text-sm text-gray-500 mb-4">Bookable in 30-minute slots. Times must be on the hour or half hour.</p>
          <form onSubmit={weekly.handleSubmit(v => save.mutate(v))} className="space-y-3">
            {fields.length === 0 && <p className="text-sm text-gray-500">No hours set. Patients cannot book online.</p>}
            {fields.map((f, i) => (
              <div key={f.id} className="flex flex-wrap items-start gap-3">
                <select className="input w-40" {...weekly.register(`windows.${i}.dayOfWeek`)}>
                  {DAYS.map(d => <option key={d} value={d}>{d}</option>)}
                </select>
                <div>
                  <input className="input w-28" type="time" step={1800} {...weekly.register(`windows.${i}.startTime`)} />
                  {weekly.formState.errors.windows?.[i]?.startTime && <p className="text-red-500 text-xs mt-1">{weekly.formState.errors.windows[i]?.startTime?.message}</p>}
                </div>
                <span className="pt-2 text-gray-500">to</span>
                <div>
                  <input className="input w-28" type="time" step={1800} {...weekly.register(`windows.${i}.endTime`)} />
                  {weekly.formState.errors.windows?.[i]?.endTime && <p className="text-red-500 text-xs mt-1">{weekly.formState.errors.windows[i]?.endTime?.message}</p>}
                </div>
                <button type="button" className="btn-secondary" aria-label="Remove window" onClick={() => remove(i)}>
                  <Trash2 size={15} />
                </button>
              </div>
            ))}
            <div className="flex gap-3 pt-2">
              <button type="button" className="btn-secondary inline-flex items-center gap-1.5"
                onClick={() => append({ dayOfWeek: 'Monday', startTime: '09:00', endTime: '17:00' })}>
                <Plus size={15} /> Add window
              </button>
              <button type="submit" className="btn-primary" disabled={save.isPending}>
                {save.isPending ? <Spinner className="w-4 h-4" /> : 'Save weekly hours'}
              </button>
            </div>
          </form>
        </section>

        <section className="card p-5">
          <h2 className="text-base font-semibold text-gray-900 mb-4">Blocked dates</h2>
          <form onSubmit={blocked.handleSubmit(v => addBlocked.mutate({ date: v.date, label: v.label || undefined }, { onSuccess: () => blocked.reset() }))}
            className="flex flex-wrap items-start gap-3 mb-4">
            <div>
              <input className="input" type="date" {...blocked.register('date')} />
              {blocked.formState.errors.date && <p className="text-red-500 text-xs mt-1">{blocked.formState.errors.date.message}</p>}
            </div>
            <input className="input w-64" placeholder="Label (optional)" maxLength={200} {...blocked.register('label')} />
            <button type="submit" className="btn-primary" disabled={addBlocked.isPending}>Block date</button>
          </form>
          {data?.blockedDates.length ? (
            <ul className="divide-y divide-gray-100">
              {data.blockedDates.map(b => (
                <li key={b.id} className="py-2 flex items-center justify-between">
                  <span className="text-sm text-gray-900">{fmt.date(b.date)}{b.label ? ` · ${b.label}` : ''}</span>
                  <button className="btn-secondary" aria-label="Unblock date" onClick={() => removeBlocked.mutate(b.id)}>
                    <Trash2 size={15} />
                  </button>
                </li>
              ))}
            </ul>
          ) : <p className="text-sm text-gray-500">No blocked dates.</p>}
        </section>
      </div>
    </>
  )
}
