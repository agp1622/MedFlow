import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { intakeApi } from '@/api/services'
import { useIntakeSubmissions, useIntakeSubmission, useDecideIntake } from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { Badge, EmptyState, PageSpinner, Pagination, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import type { IntakeAnswers, IntakeStatus } from '@/types'

// ── Public intake form (patients, reached from the emailed link) ─────────────
const optional = (max: number, label: string) => z.string().max(max, `${label} must be at most ${max} characters`).optional()

const intakeSchema = z.object({
  firstName: z.string().min(1, 'Required').max(100),
  lastName: z.string().min(1, 'Required').max(100),
  dateOfBirth: z.string().min(1, 'Required')
    .refine(v => !v || new Date(v) <= new Date(), 'Date of birth cannot be in the future'),
  gender: z.enum(['Male', 'Female', 'NonBinary', 'PreferNotToSay'], { errorMap: () => ({ message: 'Required' }) }),
  phone: z.string().min(1, 'Required').max(30),
  address: optional(200, 'Address'), city: optional(100, 'City'), state: optional(100, 'State'), zipCode: optional(20, 'ZIP code'),
  insuranceProvider: optional(200, 'Insurance provider'), insurancePolicyNumber: optional(100, 'Policy number'),
  primaryCondition: optional(500, 'Primary condition'), allergies: optional(1000, 'Allergies'),
  currentMedications: optional(2000, 'Current medications'), pastHistory: optional(2000, 'Past history'),
  additionalNotes: optional(2000, 'Additional notes'),
  consentAgreed: z.boolean().refine(v => v, 'You must agree to continue'),
  signatureName: z.string().trim().min(1, 'Type your full name to sign').max(200),
})
type IntakeForm = z.infer<typeof intakeSchema>

function Shell({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-surface px-4 py-10">
      <div className="w-full max-w-2xl mx-auto">
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-4">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center text-white font-bold">✚</div>
            <span className="text-2xl font-bold text-gray-900">MedFlow</span>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">{title}</h1>
          {subtitle && <p className="text-gray-500 text-sm mt-1">{subtitle}</p>}
        </div>
        <div className="card p-8">{children}</div>
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

export function IntakeFormPage() {
  const { token = '' } = useParams<{ token: string }>()
  const [done, setDone] = useState(false)
  const info = useQuery({ queryKey: ['intake-form', token], queryFn: () => intakeApi.getForm(token), retry: false })
  const { register, handleSubmit, formState: { errors } } = useForm<IntakeForm>({ resolver: zodResolver(intakeSchema) })

  const submit = useMutation({
    mutationFn: (d: IntakeForm) => intakeApi.submit(token, {
      ...d,
      // empty optional inputs are sent as absent
      address: d.address || undefined, city: d.city || undefined, state: d.state || undefined, zipCode: d.zipCode || undefined,
      insuranceProvider: d.insuranceProvider || undefined, insurancePolicyNumber: d.insurancePolicyNumber || undefined,
      primaryCondition: d.primaryCondition || undefined, allergies: d.allergies || undefined,
      currentMedications: d.currentMedications || undefined, pastHistory: d.pastHistory || undefined,
      additionalNotes: d.additionalNotes || undefined,
    }),
    onSuccess: () => setDone(true),
  })

  if (info.isLoading) return <Shell title="Patient intake form"><div className="flex justify-center"><Spinner className="w-5 h-5" /></div></Shell>

  if (info.isError || !info.data) {
    return (
      <Shell title="Patient intake form">
        <div className="text-center space-y-2">
          <p className="text-sm text-gray-700">This link is invalid or has expired.</p>
          <p className="text-sm text-gray-500">Please ask your doctor's office to send you a new one.</p>
        </div>
      </Shell>
    )
  }

  if (done) {
    return (
      <Shell title="Thank you">
        <p className="text-sm text-gray-700 text-center">Your form has been submitted. Your doctor will review it before your visit.</p>
      </Shell>
    )
  }

  const resp = (submit.error as any)?.response
  const serverErrors: string[] = resp?.data?.errors ?? []
  const linkGone = resp?.status === 404
  const limited = resp?.status === 429

  return (
    <Shell title={`Welcome, ${info.data.firstName}`} subtitle="Please complete this form before your first visit">
      <form onSubmit={handleSubmit(d => submit.mutate(d))} className="space-y-6" noValidate>
        <section className="space-y-4">
          <h2 className="font-semibold text-gray-900">About you</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Field label="First name" error={errors.firstName?.message}><input className="input" {...register('firstName')} /></Field>
            <Field label="Last name" error={errors.lastName?.message}><input className="input" {...register('lastName')} /></Field>
            <Field label="Date of birth" error={errors.dateOfBirth?.message}><input className="input" type="date" {...register('dateOfBirth')} /></Field>
            <Field label="Gender" error={errors.gender?.message}>
              <select className="input" defaultValue="" {...register('gender')}>
                <option value="" disabled>Select...</option>
                <option value="Male">Male</option>
                <option value="Female">Female</option>
                <option value="NonBinary">Non-binary</option>
                <option value="PreferNotToSay">Prefer not to say</option>
              </select>
            </Field>
            <Field label="Phone" error={errors.phone?.message}><input className="input" type="tel" {...register('phone')} /></Field>
            <Field label="Address" error={errors.address?.message}><input className="input" {...register('address')} /></Field>
            <Field label="City" error={errors.city?.message}><input className="input" {...register('city')} /></Field>
            <Field label="State" error={errors.state?.message}><input className="input" {...register('state')} /></Field>
            <Field label="ZIP code" error={errors.zipCode?.message}><input className="input" {...register('zipCode')} /></Field>
            <Field label="Insurance provider" error={errors.insuranceProvider?.message}><input className="input" {...register('insuranceProvider')} /></Field>
            <Field label="Policy number" error={errors.insurancePolicyNumber?.message}><input className="input" {...register('insurancePolicyNumber')} /></Field>
          </div>
        </section>

        <section className="space-y-4">
          <h2 className="font-semibold text-gray-900">Medical history</h2>
          <Field label="Main health condition" error={errors.primaryCondition?.message}><input className="input" {...register('primaryCondition')} /></Field>
          <Field label="Allergies" error={errors.allergies?.message}><textarea className="input" rows={2} {...register('allergies')} /></Field>
          <Field label="Current medications" error={errors.currentMedications?.message}><textarea className="input" rows={3} {...register('currentMedications')} /></Field>
          <Field label="Past illnesses, surgeries and family history" error={errors.pastHistory?.message}><textarea className="input" rows={3} {...register('pastHistory')} /></Field>
          <Field label="Anything else your doctor should know" error={errors.additionalNotes?.message}><textarea className="input" rows={2} {...register('additionalNotes')} /></Field>
        </section>

        <section className="space-y-4">
          <h2 className="font-semibold text-gray-900">Consent</h2>
          <p className="text-sm text-gray-700 bg-gray-50 rounded-lg p-4">{info.data.consentText}</p>
          <label className="flex items-start gap-2 text-sm text-gray-700">
            <input type="checkbox" className="mt-1" {...register('consentAgreed')} />
            <span>I have read and agree to the statement above.</span>
          </label>
          {errors.consentAgreed && <p className="text-red-500 text-xs">{errors.consentAgreed.message}</p>}
          <Field label="Signature (type your full name)" error={errors.signatureName?.message}>
            <input className="input" autoComplete="off" {...register('signatureName')} />
          </Field>
        </section>

        {linkGone && <p className="text-sm text-red-600">This link is invalid or has expired. Please ask your doctor's office for a new one.</p>}
        {limited && <p className="text-sm text-red-600">Too many requests. Please try again later.</p>}
        {serverErrors.length > 0 && <ul className="text-sm text-red-600 list-disc pl-5">{serverErrors.map(e => <li key={e}>{e}</li>)}</ul>}

        <button type="submit" className="btn-primary w-full h-10" disabled={submit.isPending}>
          {submit.isPending ? <Spinner className="w-4 h-4" /> : 'Submit form'}
        </button>
      </form>
    </Shell>
  )
}

// ── Doctor: review list ──────────────────────────────────────────────────────
const FILTERS: IntakeStatus[] = ['Pending', 'Accepted', 'Rejected']

export function IntakeListPage() {
  const navigate = useNavigate()
  const [status, setStatus] = useState<IntakeStatus>('Pending')
  const [page, setPage] = useState(1)
  const { data, isLoading } = useIntakeSubmissions(status, page)

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title="Intake forms" subtitle="Review patient answers before they enter the record" />
      <div className="flex-1 overflow-auto px-8 py-6 space-y-4">
        <div className="flex gap-2">
          {FILTERS.map(f => (
            <button key={f} className={status === f ? 'btn-primary text-xs' : 'btn-secondary text-xs'}
              onClick={() => { setStatus(f); setPage(1) }}>{f}</button>
          ))}
        </div>
        {isLoading ? <PageSpinner /> : !data?.items.length ? (
          <EmptyState title={`No ${status.toLowerCase()} submissions`} description="Send an intake form from a patient's page." />
        ) : (
          <div className="card overflow-hidden">
            <table className="w-full text-sm">
              <thead><tr className="text-left text-gray-500 border-b border-border">
                <th className="px-4 py-3">Patient</th><th className="px-4 py-3">Submitted</th><th className="px-4 py-3">Status</th>
              </tr></thead>
              <tbody>
                {data.items.map(s => (
                  <tr key={s.id} className="border-b border-border last:border-0 hover:bg-gray-50 cursor-pointer"
                    onClick={() => navigate(`/intake/review/${s.id}`)}>
                    <td className="px-4 py-3 font-medium text-gray-800">{s.patientName}</td>
                    <td className="px-4 py-3 text-gray-500">{fmt.date(s.submittedAt)}</td>
                    <td className="px-4 py-3"><Badge status={s.status} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={page} totalPages={data.totalPages} onPage={setPage} />
          </div>
        )}
      </div>
    </div>
  )
}

// ── Doctor: review one submission ────────────────────────────────────────────
const ROWS: [string, keyof IntakeAnswers][] = [
  ['First name', 'firstName'], ['Last name', 'lastName'], ['Date of birth', 'dateOfBirth'], ['Gender', 'gender'],
  ['Phone', 'phone'], ['Address', 'address'], ['City', 'city'], ['State', 'state'], ['ZIP code', 'zipCode'],
  ['Insurance provider', 'insuranceProvider'], ['Policy number', 'insurancePolicyNumber'],
  ['Primary condition', 'primaryCondition'], ['Allergies', 'allergies'],
  ['Current medications', 'currentMedications'], ['Past history', 'pastHistory'], ['Additional notes', 'additionalNotes'],
]

export function IntakeReviewPage() {
  const { id } = useParams<{ id: string }>()
  const submissionId = Number(id)
  const navigate = useNavigate()
  const { data, isLoading } = useIntakeSubmission(submissionId)
  const decide = useDecideIntake(submissionId)
  const [reason, setReason] = useState('')

  if (isLoading) return <PageSpinner />
  if (!data) return <div className="p-8 text-gray-500">Submission not found</div>
  const pending = data.status === 'Pending'
  const show = (v?: string | null) => (v && v.trim() ? v : '—')

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title="Intake form review" subtitle={`Submitted ${fmt.date(data.submittedAt)}`}>
        <button className="btn-ghost gap-1" onClick={() => navigate('/intake')}><ArrowLeft size={15} /> Back</button>
      </PageHeader>
      <div className="flex-1 overflow-auto px-8 py-6 space-y-5">
        <div className="card p-5">
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-semibold text-gray-900">Answers compared with the current record</h2>
            <Badge status={data.status} />
          </div>
          <table className="w-full text-sm">
            <thead><tr className="text-left text-gray-500"><th className="py-2 w-1/4">Field</th><th>Submitted answer</th><th>Current record</th></tr></thead>
            <tbody>
              {ROWS.map(([label, key]) => {
                const a = data.answers[key] as string | null | undefined
                const c = data.current?.[key] as string | null | undefined
                const changed = (a ?? '') !== (c ?? '')
                return (
                  <tr key={key} className="border-t border-border">
                    <td className="py-2 text-gray-500">{label}</td>
                    <td className={`py-2 whitespace-pre-wrap ${changed ? 'font-medium text-gray-900' : 'text-gray-600'}`}>{show(a)}</td>
                    <td className="py-2 text-gray-500 whitespace-pre-wrap">
                      {key === 'currentMedications' || key === 'pastHistory' || key === 'additionalNotes' ? 'Added to Notes' : show(c)}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>

        <div className="card p-5 text-sm space-y-1">
          <h2 className="font-semibold text-gray-900 mb-2">Consent</h2>
          <p><span className="text-gray-500">Signed as:</span> {data.consent.signatureName}</p>
          <p><span className="text-gray-500">Signed at:</span> {new Date(data.consent.signedAt).toLocaleString()}</p>
          <p><span className="text-gray-500">Consent text version:</span> {data.consent.consentVersion}</p>
          <p><span className="text-gray-500">Agreement:</span> {data.consent.consentAgreed ? 'Agreed' : 'Not agreed'}</p>
        </div>

        {pending ? (
          <div className="card p-5 space-y-3">
            <p className="text-sm text-gray-600">Accepting updates the patient record with these answers. Rejecting leaves the record unchanged.</p>
            <input className="input" placeholder="Reason for rejecting (optional)" maxLength={500}
              value={reason} onChange={e => setReason(e.target.value)} />
            <div className="flex gap-2">
              <button className="btn-primary" disabled={decide.isPending} onClick={() => decide.mutate({ accept: true })}>Accept</button>
              <button className="btn-secondary" disabled={decide.isPending} onClick={() => decide.mutate({ accept: false, reason })}>Reject</button>
            </div>
          </div>
        ) : (
          <p className="text-sm text-gray-500">
            {data.status} {data.decidedAt ? fmt.date(data.decidedAt) : ''}{data.rejectionReason ? ` — ${data.rejectionReason}` : ''}
          </p>
        )}
      </div>
    </div>
  )
}
