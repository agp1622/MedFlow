import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import {
  usePrescriptions, useCreatePrescription, useDeletePrescription,
  useInvoices, useCreateInvoice, useMarkInvoicePaid,
  usePatients
} from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { Avatar, Badge, StatCard, PageSpinner, EmptyState, SearchInput, Pagination, Spinner } from '@/components/ui'
import { Modal } from './PatientsPage'
import { fmt, displayEnum } from '@/utils/format'
import { Trash2, CheckCircle } from 'lucide-react'
import type { CreatePrescriptionRequest, CreateInvoiceRequest } from '@/types'

// ── Prescriptions Page ────────────────────────────────────────────────────────
export function PrescriptionsPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const { data, isLoading } = usePrescriptions({ page, pageSize: 20, search })
  const deletePrescription = useDeletePrescription()

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title="Prescriptions" subtitle={`${data?.totalCount ?? 0} total`}
        action={{ label: 'New Prescription', onClick: () => setShowModal(true) }}>
        <SearchInput value={search} onChange={v => { setSearch(v); setPage(1) }} placeholder="Search drug or patient..." />
      </PageHeader>

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-8 py-6">
          <div className="card overflow-hidden">
            <table className="w-full">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {['Patient', 'Drug', 'Dosage', 'Frequency', 'Issued', 'Expires', 'Refills', 'Status', ''].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.length === 0 && (
                  <tr><td colSpan={9}><EmptyState title="No prescriptions" description="Create your first prescription" /></td></tr>
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
                    <td className="px-4 py-3">
                      <button className="btn-ghost p-1.5"
                        onClick={() => { if (confirm('Delete prescription?')) deletePrescription.mutate(rx.id) }}>
                        <Trash2 size={14} className="text-gray-400" />
                      </button>
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
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const { data, isLoading } = useInvoices({ page, pageSize: 20, search })
  const markPaid = useMarkInvoicePaid()

  const paid = data?.items.filter(i => i.status === 'Paid').reduce((s, i) => s + i.amount, 0) ?? 0
  const pending = data?.items.filter(i => i.status === 'Pending').reduce((s, i) => s + i.amount, 0) ?? 0
  const overdue = data?.items.filter(i => i.status === 'Overdue').reduce((s, i) => s + i.amount, 0) ?? 0

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title="Billing" subtitle={`${data?.totalCount ?? 0} invoices`}
        action={{ label: 'New Invoice', onClick: () => setShowModal(true) }}>
        <SearchInput value={search} onChange={v => { setSearch(v); setPage(1) }} placeholder="Search invoices..." />
      </PageHeader>

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-8 py-6 space-y-5">
          <div className="flex gap-4 flex-wrap">
            <StatCard icon="✅" label="Collected" value={fmt.currency(paid)} color="text-emerald-600" />
            <StatCard icon="⏳" label="Pending" value={fmt.currency(pending)} color="text-amber-600" />
            <StatCard icon="⚠️" label="Overdue" value={fmt.currency(overdue)} color="text-red-500" />
          </div>

          <div className="card overflow-hidden">
            <table className="w-full">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {['Invoice #', 'Patient', 'Date', 'Service', 'Amount', 'Due Date', 'Status', 'Actions'].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.length === 0 && (
                  <tr><td colSpan={8}><EmptyState title="No invoices" description="Create your first invoice" /></td></tr>
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
                    <td className="px-4 py-3">
                      {inv.status !== 'Paid' && inv.status !== 'Cancelled' && (
                        <button className="btn-secondary p-1.5 gap-1 text-xs"
                          onClick={() => markPaid.mutate(inv.id)} title="Mark as paid">
                          <CheckCircle size={14} /> Paid
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
  patientId: z.coerce.number().min(1, 'Select a patient'),
  drugName: z.string().min(1, 'Required'),
  dosage: z.string().min(1, 'Required'),
  frequency: z.string().min(1, 'Required'),
  instructions: z.string().optional(),
  issuedDate: z.string().min(1, 'Required'),
  expiryDate: z.string().min(1, 'Required'),
  refillsRemaining: z.coerce.number().min(0).max(99),
})
type RxForm = z.infer<typeof rxSchema>

function NewPrescriptionModal({ onClose }: { onClose: () => void }) {
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
    <Modal title="New Prescription" onClose={onClose}>
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
            <label className="label">Drug name</label>
            <input className="input" placeholder="e.g. Lisinopril" {...register('drugName')} />
            {errors.drugName && <p className="text-red-500 text-xs mt-1">{errors.drugName.message}</p>}
          </div>
          <div>
            <label className="label">Dosage</label>
            <input className="input" placeholder="e.g. 10mg" {...register('dosage')} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Frequency</label>
            <input className="input" placeholder="e.g. Once daily" {...register('frequency')} />
          </div>
          <div>
            <label className="label">Refills remaining</label>
            <input className="input" type="number" min={0} {...register('refillsRemaining')} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Issued date</label>
            <input className="input" type="date" {...register('issuedDate')} />
          </div>
          <div>
            <label className="label">Expiry date</label>
            <input className="input" type="date" {...register('expiryDate')} />
          </div>
        </div>
        <div>
          <label className="label">Instructions (optional)</label>
          <textarea className="input resize-none" rows={2} placeholder="Take with food..." {...register('instructions')} />
        </div>
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn-primary" disabled={create.isPending}>
            {create.isPending ? <Spinner className="w-4 h-4" /> : 'Create Prescription'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ── New Invoice Modal ─────────────────────────────────────────────────────────
const invSchema = z.object({
  patientId: z.coerce.number().min(1, 'Select a patient'),
  serviceDescription: z.string().min(1, 'Required'),
  amount: z.coerce.number().min(0.01, 'Must be > 0'),
  dueDate: z.string().optional(),
  notes: z.string().optional(),
})
type InvForm = z.infer<typeof invSchema>

function NewInvoiceModal({ onClose }: { onClose: () => void }) {
  const { register, handleSubmit, formState: { errors } } = useForm<InvForm>({ resolver: zodResolver(invSchema) })
  const create = useCreateInvoice()
  const { data: patients } = usePatients({ pageSize: 200 })

  const onSubmit = (data: InvForm) => {
    create.mutate(data as unknown as CreateInvoiceRequest, { onSuccess: onClose })
  }

  return (
    <Modal title="New Invoice" onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label className="label">Patient</label>
          <select className="input" {...register('patientId')}>
            <option value="">Select patient...</option>
            {patients?.items.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
          </select>
          {errors.patientId && <p className="text-red-500 text-xs mt-1">{errors.patientId.message}</p>}
        </div>
        <div>
          <label className="label">Service description</label>
          <input className="input" placeholder="e.g. Consultation + Labs" {...register('serviceDescription')} />
          {errors.serviceDescription && <p className="text-red-500 text-xs mt-1">{errors.serviceDescription.message}</p>}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Amount ($)</label>
            <input className="input" type="number" step="0.01" min="0" placeholder="0.00" {...register('amount')} />
            {errors.amount && <p className="text-red-500 text-xs mt-1">{errors.amount.message}</p>}
          </div>
          <div>
            <label className="label">Due date</label>
            <input className="input" type="date" {...register('dueDate')} />
          </div>
        </div>
        <div>
          <label className="label">Notes (optional)</label>
          <textarea className="input resize-none" rows={2} {...register('notes')} />
        </div>
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn-primary" disabled={create.isPending}>
            {create.isPending ? <Spinner className="w-4 h-4" /> : 'Create Invoice'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
