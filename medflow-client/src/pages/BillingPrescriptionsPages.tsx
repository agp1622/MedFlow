import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ParseKeys } from 'i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import {
  usePrescriptions, useCreatePrescription, useDeletePrescription,
  useInvoices, useCreateInvoice, useMarkInvoicePaid, useExportClaimDraft,
  usePatients
} from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { useAuthStore } from '@/store/authStore'
import { can } from '@/utils/permissions'
import { Avatar, Badge, StatCard, PageSpinner, EmptyState, SearchInput, Pagination, Spinner } from '@/components/ui'
import { Modal } from './PatientsPage'
import { PrintPrescriptionButton } from '@/components/prescriptions/PrintPrescriptionButton'
import { fmt } from '@/utils/format'
import { Trash2, CheckCircle, FileDown } from 'lucide-react'
import type { CreatePrescriptionRequest, CreateInvoiceRequest } from '@/types'

// ── Prescriptions Page ────────────────────────────────────────────────────────
export function PrescriptionsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const { data, isLoading } = usePrescriptions({ page, pageSize: 20, search })
  const deletePrescription = useDeletePrescription()
  const canWrite = can(useAuthStore(s => s.user?.role), 'PrescriptionsWrite') // Nurses read and print only

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={t('prescriptions.title')} subtitle={t('prescriptions.total', { count: data?.totalCount ?? 0 })}
        action={canWrite ? { label: t('prescriptions.new'), onClick: () => setShowModal(true) } : undefined}>
        <SearchInput value={search} onChange={v => { setSearch(v); setPage(1) }} placeholder={t('prescriptions.search')} />
      </PageHeader>

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6">
          <div className="card overflow-x-auto">
            <table className="w-full min-w-[640px]">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {(['patient', 'drug', 'dosage', 'frequency', 'issued', 'expires', 'refills', 'status', ''] as const).map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide whitespace-nowrap">{h && t(`prescriptions.cols.${h}`)}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.length === 0 && (
                  <tr><td colSpan={9}><EmptyState title={t('prescriptions.empty')} description={t('prescriptions.emptyHint')} /></td></tr>
                )}
                {data?.items.map(rx => (
                  <tr key={rx.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2.5 cursor-pointer" onClick={() => navigate(`/patients/${rx.patientId}`)}>
                        <Avatar name={rx.patientName} size="sm" />
                        <span className="font-semibold text-gray-900 text-sm">{rx.patientName}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 font-semibold text-gray-900">{rx.drugName}</td>
                    <td className="px-4 py-3 text-sm text-gray-700">{rx.dosage}</td>
                    <td className="px-4 py-3 text-sm text-gray-700">{rx.frequency}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{fmt.date(rx.issuedDate)}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{fmt.date(rx.expiryDate)}</td>
                    <td className="px-4 py-3 text-sm text-center text-gray-700">{rx.refillsRemaining}</td>
                    <td className="px-4 py-3"><Badge status={rx.status} /></td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <PrintPrescriptionButton id={rx.id} />
                      {canWrite && <button className="btn-ghost p-1.5"
                        onClick={() => { if (confirm(t('prescriptions.confirmDelete'))) deletePrescription.mutate(rx.id) }}>
                        <Trash2 size={14} className="text-gray-400" />
                      </button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={page} totalPages={data?.totalPages ?? 1} onPage={setPage} />
          </div>
        </div>
      )}
      {showModal && <NewPrescriptionModal onClose={() => setShowModal(false)} />}
    </div>
  )
}

// ── Billing Page ──────────────────────────────────────────────────────────────
export function BillingPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const { data, isLoading } = useInvoices({ page, pageSize: 20, search })
  const markPaid = useMarkInvoicePaid()
  const exportClaim = useExportClaimDraft()

  const paid = data?.items.filter(i => i.status === 'Paid').reduce((s, i) => s + i.amount, 0) ?? 0
  const pending = data?.items.filter(i => i.status === 'Pending').reduce((s, i) => s + i.amount, 0) ?? 0
  const overdue = data?.items.filter(i => i.status === 'Overdue').reduce((s, i) => s + i.amount, 0) ?? 0

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={t('billing.title')} subtitle={t('billing.total', { count: data?.totalCount ?? 0 })}
        action={{ label: t('billing.new'), onClick: () => setShowModal(true) }}>
        <SearchInput value={search} onChange={v => { setSearch(v); setPage(1) }} placeholder={t('billing.search')} />
      </PageHeader>

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6 space-y-5">
          <div className="flex gap-4 flex-wrap">
            <StatCard icon="✅" label={t('billing.collected')} value={fmt.currency(paid)} color="text-emerald-600" />
            <StatCard icon="⏳" label={t('billing.pending')} value={fmt.currency(pending)} color="text-amber-600" />
            <StatCard icon="⚠️" label={t('billing.overdue')} value={fmt.currency(overdue)} color="text-red-500" />
          </div>

          <div className="card overflow-x-auto">
            <table className="w-full min-w-[640px]">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {(['number', 'patient', 'date', 'service', 'amount', 'dueDate', 'status', 'actions'] as const).map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide whitespace-nowrap">{t(`billing.cols.${h}`)}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.length === 0 && (
                  <tr><td colSpan={8}><EmptyState title={t('billing.empty')} description={t('billing.emptyHint')} /></td></tr>
                )}
                {data?.items.map(inv => (
                  <tr key={inv.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-bold text-primary-600 text-sm">{inv.invoiceNumber}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2.5 cursor-pointer" onClick={() => navigate(`/patients/${inv.patientId}`)}>
                        <Avatar name={inv.patientName} size="sm" />
                        <span className="font-semibold text-gray-900 text-sm">{inv.patientName}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">{fmt.date(inv.invoiceDate)}</td>
                    <td className="px-4 py-3 text-sm text-gray-700 max-w-36 truncate">{inv.serviceDescription}</td>
                    <td className="px-4 py-3 font-bold text-gray-900">{fmt.currency(inv.amount)}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{fmt.date(inv.dueDate)}</td>
                    <td className="px-4 py-3"><Badge status={inv.status} /></td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <span className="inline-flex gap-1 mr-1">
                        {(['json', 'csv'] as const).map(f => (
                          <button key={f} className="btn-secondary p-1.5 gap-1 text-xs" disabled={exportClaim.isPending}
                            onClick={() => exportClaim.mutate({ id: inv.id, format: f })}
                            title={t('billing.claim.exportTitle', { format: f.toUpperCase() })}
                            aria-label={t('billing.claim.exportTitle', { format: f.toUpperCase() })}>
                            <FileDown size={14} /> {f.toUpperCase()}
                          </button>
                        ))}
                      </span>
                      {inv.status !== 'Paid' && inv.status !== 'Cancelled' && (
                        <button className="btn-secondary p-1.5 gap-1 text-xs"
                          onClick={() => markPaid.mutate(inv.id)} title={t('billing.markPaid')}>
                          <CheckCircle size={14} /> {t('billing.paid')}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={page} totalPages={data?.totalPages ?? 1} onPage={setPage} />
          </div>
        </div>
      )}
      {showModal && <NewInvoiceModal onClose={() => setShowModal(false)} />}
    </div>
  )
}

// ── New Prescription Modal ────────────────────────────────────────────────────
const rxSchema = z.object({
  patientId: z.coerce.number().min(1, 'appointments.selectPatient'),
  drugName: z.string().min(1, 'validation.required'),
  dosage: z.string().min(1, 'validation.required'),
  frequency: z.string().min(1, 'validation.required'),
  instructions: z.string().optional(),
  issuedDate: z.string().min(1, 'validation.required'),
  expiryDate: z.string().min(1, 'validation.required'),
  refillsRemaining: z.coerce.number().min(0).max(99),
})
type RxForm = z.infer<typeof rxSchema>

function NewPrescriptionModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const { register, handleSubmit, formState: { errors } } = useForm<RxForm>({
    resolver: zodResolver(rxSchema),
    defaultValues: { refillsRemaining: 1, issuedDate: new Date().toISOString().split('T')[0] }
  })
  const create = useCreatePrescription()
  const { data: patients } = usePatients({ pageSize: 200 })

  const onSubmit = (data: RxForm) => {
    create.mutate(data as unknown as CreatePrescriptionRequest, { onSuccess: onClose })
  }

  return (
    <Modal title={t('prescriptions.new')} onClose={onClose}>
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
            <label className="label">{t('prescriptions.form.drug')}</label>
            <input className="input" placeholder={t('prescriptions.form.drugPlaceholder')} {...register('drugName')} />
            {errors.drugName && <p className="text-red-500 text-xs mt-1">{t(errors.drugName.message as ParseKeys)}</p>}
          </div>
          <div>
            <label className="label">{t('prescriptions.form.dosage')}</label>
            <input className="input" placeholder={t('prescriptions.form.dosagePlaceholder')} {...register('dosage')} />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="label">{t('prescriptions.form.frequency')}</label>
            <input className="input" placeholder={t('prescriptions.form.frequencyPlaceholder')} {...register('frequency')} />
          </div>
          <div>
            <label className="label">{t('prescriptions.form.refills')}</label>
            <input className="input" type="number" min={0} {...register('refillsRemaining')} />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="label">{t('prescriptions.form.issued')}</label>
            <input className="input" type="date" {...register('issuedDate')} />
          </div>
          <div>
            <label className="label">{t('prescriptions.form.expiry')}</label>
            <input className="input" type="date" {...register('expiryDate')} />
          </div>
        </div>
        <div>
          <label className="label">{t('prescriptions.form.instructions')}</label>
          <textarea className="input resize-none" rows={2} placeholder={t('prescriptions.form.instructionsPlaceholder')} {...register('instructions')} />
        </div>
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="btn-primary" disabled={create.isPending}>
            {create.isPending ? <Spinner className="w-4 h-4" /> : t('prescriptions.form.create')}
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ── New Invoice Modal ─────────────────────────────────────────────────────────
const invSchema = z.object({
  patientId: z.coerce.number().min(1, 'appointments.selectPatient'),
  serviceDescription: z.string().min(1, 'validation.required'),
  amount: z.coerce.number().min(0.01, 'billing.amountPositive'),
  dueDate: z.string().optional(),
  notes: z.string().optional(),
})
type InvForm = z.infer<typeof invSchema>

function NewInvoiceModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const { register, handleSubmit, formState: { errors } } = useForm<InvForm>({ resolver: zodResolver(invSchema) })
  const create = useCreateInvoice()
  const { data: patients } = usePatients({ pageSize: 200 })

  const onSubmit = (data: InvForm) => {
    create.mutate(data as unknown as CreateInvoiceRequest, { onSuccess: onClose })
  }

  return (
    <Modal title={t('billing.new')} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label className="label">{t('appointments.form.patient')}</label>
          <select className="input" {...register('patientId')}>
            <option value="">{t('appointments.form.selectPatient')}</option>
            {patients?.items.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
          </select>
          {errors.patientId && <p className="text-red-500 text-xs mt-1">{t(errors.patientId.message as ParseKeys)}</p>}
        </div>
        <div>
          <label className="label">{t('billing.form.service')}</label>
          <input className="input" placeholder={t('billing.form.servicePlaceholder')} {...register('serviceDescription')} />
          {errors.serviceDescription && <p className="text-red-500 text-xs mt-1">{t(errors.serviceDescription.message as ParseKeys)}</p>}
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="label">{t('billing.form.amount')}</label>
            <input className="input" type="number" step="0.01" min="0" placeholder="0.00" {...register('amount')} />
            {errors.amount && <p className="text-red-500 text-xs mt-1">{t(errors.amount.message as ParseKeys)}</p>}
          </div>
          <div>
            <label className="label">{t('billing.form.dueDate')}</label>
            <input className="input" type="date" {...register('dueDate')} />
          </div>
        </div>
        <div>
          <label className="label">{t('billing.form.notes')}</label>
          <textarea className="input resize-none" rows={2} {...register('notes')} />
        </div>
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="btn-primary" disabled={create.isPending}>
            {create.isPending ? <Spinner className="w-4 h-4" /> : t('billing.form.create')}
          </button>
        </div>
      </form>
    </Modal>
  )
}
