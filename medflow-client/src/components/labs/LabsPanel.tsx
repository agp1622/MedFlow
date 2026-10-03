import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Pencil, Plus, Trash2, Ban, AlertTriangle } from 'lucide-react'
import {
  usePatientLabs, useSaveLabOrder, useCancelLabOrder, useDeleteLabOrder, useSaveLabResult, useDeleteLabResult,
} from '@/hooks/queries'
import { tError, currentLanguage } from '@/i18n'
import { Badge, EmptyState, PageSpinner, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import type { LabOrderDto, LabResultDto } from '@/types'

const max = (field: string, n: number) => `validation.maxLength?${JSON.stringify({ field, max: n })}`
const REQUIRED = 'validation.required'
const NUMBER = 'labs.number'

const orderSchema = z.object({
  testName: z.string().trim().min(1, REQUIRED).max(150, max('labs.testName', 150)),
  notes: z.string().max(1000, max('labs.notes', 1000)).optional(),
  orderedDate: z.string().optional(),
})

const isNumber = (s?: string) => !s || s.trim() === '' || Number.isFinite(Number(s))
const resultSchema = z.object({
  analyteName: z.string().trim().min(1, REQUIRED).max(100, max('labs.analyte', 100)),
  value: z.string().trim().min(1, REQUIRED).refine(isNumber, NUMBER),
  unit: z.string().max(30, max('labs.unit', 30)).optional(),
  referenceLow: z.string().optional().refine(isNumber, NUMBER),
  referenceHigh: z.string().optional().refine(isNumber, NUMBER),
}).refine(
  d => !d.referenceLow?.trim() || !d.referenceHigh?.trim() || Number(d.referenceLow) <= Number(d.referenceHigh),
  { message: 'labs.rangeOrder', path: ['referenceLow'] },
)

const blank = (s?: string) => (s && s.trim() ? s.trim() : undefined)
const optNum = (s?: string) => (s && s.trim() ? Number(s) : undefined)
// Clinical values keep up to 4 decimals; the default formatter would round them
const num = (n: number) => new Intl.NumberFormat(currentLanguage(), { maximumFractionDigits: 4 }).format(n)
const today = () => new Date().toISOString().slice(0, 10)

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

function FormActions({ pending, onCancel }: { pending: boolean; onCancel: () => void }) {
  const { t } = useTranslation()
  return (
    <div className="flex justify-end gap-2 pt-1">
      <button type="button" className="btn-ghost" onClick={onCancel}>{t('common.cancel')}</button>
      <button type="submit" className="btn-primary" disabled={pending}>
        {pending ? <Spinner className="w-4 h-4" /> : t('common.save')}
      </button>
    </div>
  )
}

function OrderForm({ patientId, item, onDone }: { patientId: number; item?: LabOrderDto; onDone: () => void }) {
  const { t } = useTranslation()
  const save = useSaveLabOrder(patientId)
  const { register, handleSubmit, formState: { errors } } = useForm<z.infer<typeof orderSchema>>({
    resolver: zodResolver(orderSchema),
    defaultValues: { testName: item?.testName ?? '', notes: item?.notes ?? '', orderedDate: item?.orderedDate ?? today() },
  })
  return (
    <form className="space-y-3 border border-border rounded-xl p-3 bg-gray-50"
      onSubmit={handleSubmit(d => save.mutate(
        { id: item?.id, data: { testName: d.testName.trim(), notes: blank(d.notes), orderedDate: blank(d.orderedDate) } },
        { onSuccess: onDone }))}>
      <Field label={t('labs.testName')} error={errors.testName?.message}><input className="input" {...register('testName')} /></Field>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Field label={t('labs.orderedDate')} error={errors.orderedDate?.message}>
          <input className="input" type="date" max={today()} {...register('orderedDate')} />
        </Field>
        <Field label={t('labs.notes')} error={errors.notes?.message}><input className="input" {...register('notes')} /></Field>
      </div>
      <FormActions pending={save.isPending} onCancel={onDone} />
    </form>
  )
}

function ResultForm({ patientId, orderId, item, onDone }: { patientId: number; orderId: number; item?: LabResultDto; onDone: () => void }) {
  const { t } = useTranslation()
  const save = useSaveLabResult(patientId, orderId)
  const { register, handleSubmit, formState: { errors } } = useForm<z.infer<typeof resultSchema>>({
    resolver: zodResolver(resultSchema),
    defaultValues: {
      analyteName: item?.analyteName ?? '', value: item ? String(item.value) : '', unit: item?.unit ?? '',
      referenceLow: item?.referenceLow != null ? String(item.referenceLow) : '',
      referenceHigh: item?.referenceHigh != null ? String(item.referenceHigh) : '',
    },
  })
  return (
    <form className="space-y-3 border border-border rounded-xl p-3 bg-white"
      onSubmit={handleSubmit(d => save.mutate({
        id: item?.id,
        data: {
          analyteName: d.analyteName.trim(), value: Number(d.value), unit: blank(d.unit),
          referenceLow: optNum(d.referenceLow), referenceHigh: optNum(d.referenceHigh),
        },
      }, { onSuccess: onDone }))}>
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <Field label={t('labs.analyte')} error={errors.analyteName?.message}><input className="input" {...register('analyteName')} /></Field>
        <Field label={t('labs.value')} error={errors.value?.message}><input className="input" type="number" step="any" {...register('value')} /></Field>
        <Field label={t('labs.unit')} error={errors.unit?.message}><input className="input" {...register('unit')} /></Field>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Field label={t('labs.refLow')} error={errors.referenceLow?.message}><input className="input" type="number" step="any" {...register('referenceLow')} /></Field>
        <Field label={t('labs.refHigh')} error={errors.referenceHigh?.message}><input className="input" type="number" step="any" {...register('referenceHigh')} /></Field>
      </div>
      <p className="text-xs text-gray-500">{t('labs.refHint')}</p>
      <FormActions pending={save.isPending} onCancel={onDone} />
    </form>
  )
}

/** Text label plus colour, so the flag never relies on colour alone. */
function FlagBadge({ flag }: { flag: LabResultDto['flag'] }) {
  const { t } = useTranslation()
  if (flag === 'None') return null
  return (
    <span className="badge bg-red-50 text-red-700 inline-flex items-center gap-1" data-testid={`lab-flag-${flag}`}>
      <AlertTriangle size={12} aria-hidden="true" />{t(`labs.flag.${flag}`)}
    </span>
  )
}

function OrderCard({ patientId, order }: { patientId: number; order: LabOrderDto }) {
  const { t } = useTranslation()
  const cancel = useCancelLabOrder(patientId)
  const del = useDeleteLabOrder(patientId)
  const delResult = useDeleteLabResult(patientId, order.id)
  const [editingOrder, setEditingOrder] = useState(false)
  const [resultForm, setResultForm] = useState<number | 'new' | null>(null)
  const cancelled = order.status === 'Cancelled'

  return (
    <div className={`card p-4 space-y-3 ${cancelled ? 'opacity-70' : ''}`}>
      {editingOrder ? <OrderForm patientId={patientId} item={order} onDone={() => setEditingOrder(false)} /> : (
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="font-semibold text-gray-800 break-words">
              {order.testName} <Badge status={order.status} />
              {order.abnormalCount > 0 && (
                <span className="badge bg-red-50 text-red-700 ml-1">{t('labs.abnormal', { count: order.abnormalCount })}</span>
              )}
            </p>
            <p className="text-xs text-gray-500">{t('labs.ordered', { date: fmt.date(order.orderedDate) })}</p>
            {order.notes && <p className="text-xs text-gray-500 break-words">{order.notes}</p>}
          </div>
          <div className="flex gap-1 flex-shrink-0">
            {!cancelled && (
              <>
                <button type="button" className="btn-ghost p-1" aria-label={t('labs.editOrder')} onClick={() => setEditingOrder(true)}><Pencil size={14} className="text-gray-400" /></button>
                <button type="button" className="btn-ghost p-1" aria-label={t('labs.cancelOrder')}
                  onClick={() => { if (confirm(t('labs.confirmCancel'))) cancel.mutate(order.id) }}><Ban size={14} className="text-gray-400" /></button>
              </>
            )}
            <button type="button" className="btn-ghost p-1" aria-label={t('labs.deleteOrder')}
              onClick={() => { if (confirm(t('labs.confirmDelete'))) del.mutate(order.id) }}><Trash2 size={14} className="text-gray-400" /></button>
          </div>
        </div>
      )}

      {order.results.length === 0 && resultForm !== 'new' && <p className="text-sm text-gray-400">{t('labs.noResults')}</p>}
      {order.results.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-gray-500">
                <th className="py-1 pr-3 font-medium">{t('labs.analyte')}</th>
                <th className="py-1 pr-3 font-medium">{t('labs.value')}</th>
                <th className="py-1 pr-3 font-medium">{t('labs.range')}</th>
                <th className="py-1 w-16" />
              </tr>
            </thead>
            <tbody>
              {order.results.map(r => resultForm === r.id ? (
                <tr key={r.id}><td colSpan={4}><ResultForm patientId={patientId} orderId={order.id} item={r} onDone={() => setResultForm(null)} /></td></tr>
              ) : (
                <tr key={r.id} className={`border-t border-border ${r.flag !== 'None' && !cancelled ? 'bg-red-50/50' : ''}`}>
                  <td className="py-2 pr-3 text-gray-800">{r.analyteName}</td>
                  <td className="py-2 pr-3 font-semibold text-gray-800">
                    {num(r.value)} {r.unit} {!cancelled && <FlagBadge flag={r.flag} />}
                  </td>
                  <td className="py-2 pr-3 text-gray-500">
                    {r.referenceLow == null && r.referenceHigh == null
                      ? t('labs.noRange')
                      : `${r.referenceLow != null ? num(r.referenceLow) : '…'} – ${r.referenceHigh != null ? num(r.referenceHigh) : '…'}`}
                  </td>
                  <td className="py-2 text-right">
                    {!cancelled && (
                      <span className="inline-flex gap-1">
                        <button type="button" className="btn-ghost p-1" aria-label={t('labs.editResult')} onClick={() => setResultForm(r.id)}><Pencil size={13} className="text-gray-400" /></button>
                        <button type="button" className="btn-ghost p-1" aria-label={t('common.remove')}
                          onClick={() => { if (confirm(t('labs.confirmRemoveResult'))) delResult.mutate(r.id) }}><Trash2 size={13} className="text-gray-400" /></button>
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {resultForm === 'new' && <ResultForm patientId={patientId} orderId={order.id} onDone={() => setResultForm(null)} />}
      {!cancelled && resultForm === null && (
        <button type="button" className="btn-secondary text-xs" onClick={() => setResultForm('new')}>
          <Plus size={13} className="inline mr-1" />{t('labs.addResult')}
        </button>
      )}
    </div>
  )
}

/** Lab orders with manually entered results; abnormal values are flagged from the range the doctor entered. Doctor-only. */
export function LabsPanel({ patientId }: { patientId: number }) {
  const { t } = useTranslation()
  const { data, isLoading, isError } = usePatientLabs(patientId)
  const [adding, setAdding] = useState(false)

  if (isLoading) return <PageSpinner />
  if (isError || !data) return <div className="card p-5 text-sm text-red-600">{t('labs.loadError')}</div>

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-bold text-gray-800">
          {t('labs.title')}
          {data.abnormalCount > 0 && <span className="badge bg-red-50 text-red-700 ml-2">{t('labs.abnormal', { count: data.abnormalCount })}</span>}
        </h3>
        <button type="button" className="btn-primary text-sm" onClick={() => setAdding(true)}>
          <Plus size={14} className="inline mr-1" />{t('labs.newOrder')}
        </button>
      </div>
      {adding && <OrderForm patientId={patientId} onDone={() => setAdding(false)} />}
      {data.orders.length === 0 && !adding && <EmptyState title={t('labs.empty')} description={t('labs.emptyHint')} />}
      {data.orders.map(o => <OrderCard key={o.id} patientId={patientId} order={o} />)}
    </div>
  )
}
