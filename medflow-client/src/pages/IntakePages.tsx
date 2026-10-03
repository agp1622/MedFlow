import { useState } from 'react'
import { useTranslation } from 'react-i18next'
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
import { fmt, displayEnum } from '@/utils/format'
import { tError } from '@/i18n'
import { LanguageSwitcher } from '@/components/ui/LanguageSwitcher'
import type { IntakeAnswers, IntakeStatus } from '@/types'

// ── Public intake form (patients, reached from the emailed link) ─────────────
const optional = (max: number, field: string) =>
  z.string().max(max, `validation.maxLength?${JSON.stringify({ field: `intake.fields.${field}`, max })}`).optional()

const intakeSchema = z.object({
  firstName: z.string().min(1, 'validation.required').max(100),
  lastName: z.string().min(1, 'validation.required').max(100),
  dateOfBirth: z.string().min(1, 'validation.required')
    .refine(v => !v || new Date(v) <= new Date(), 'intake.futureDob'),
  gender: z.enum(['Male', 'Female', 'NonBinary', 'PreferNotToSay'], { errorMap: () => ({ message: 'validation.required' }) }),
  phone: z.string().min(1, 'validation.required').max(30),
  address: optional(200, 'address'), city: optional(100, 'city'), state: optional(100, 'state'), zipCode: optional(20, 'zipCode'),
  insuranceProvider: optional(200, 'insuranceProvider'), insurancePolicyNumber: optional(100, 'insurancePolicyNumber'),
  primaryCondition: optional(500, 'primaryCondition'), allergies: optional(1000, 'allergies'),
  currentMedications: optional(2000, 'currentMedications'), pastHistory: optional(2000, 'pastHistory'),
  additionalNotes: optional(2000, 'additionalNotes'),
  consentAgreed: z.boolean().refine(v => v, 'intake.mustAgree'),
  signatureName: z.string().trim().min(1, 'intake.signRequired').max(200),
})
type IntakeForm = z.infer<typeof intakeSchema>

function Shell({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-surface px-4 py-10 relative">
      <LanguageSwitcher className="absolute top-3 right-3" />
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
  const { t } = useTranslation()
  return (
    <div>
      <label className="label">{label}</label>
      {children}
      {error && <p className="text-red-500 text-xs mt-1">{tError(t, error)}</p>}
    </div>
  )
}

export function IntakeFormPage() {
  const { t } = useTranslation()
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

  if (info.isLoading) return <Shell title={t('intake.public.title')}><div className="flex justify-center"><Spinner className="w-5 h-5" /></div></Shell>

  if (info.isError || !info.data) {
    return (
      <Shell title={t('intake.public.title')}>
        <div className="text-center space-y-2">
          <p className="text-sm text-gray-700">{t('intake.public.invalidLink')}</p>
          <p className="text-sm text-gray-500">{t('intake.public.askNew')}</p>
        </div>
      </Shell>
    )
  }

  if (done) {
    return (
      <Shell title={t('intake.public.thanks')}>
        <p className="text-sm text-gray-700 text-center">{t('intake.public.submitted')}</p>
      </Shell>
    )
  }

  const resp = (submit.error as any)?.response
  const serverErrors: string[] = resp?.data?.errors ?? []
  const linkGone = resp?.status === 404
  const limited = resp?.status === 429

  return (
    <Shell title={t('intake.public.welcome', { name: info.data.firstName })} subtitle={t('intake.public.subtitle')}>
      <form onSubmit={handleSubmit(d => submit.mutate(d))} className="space-y-6" noValidate>
        <section className="space-y-4">
          <h2 className="font-semibold text-gray-900">{t('intake.public.aboutYou')}</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Field label={t('intake.fields.firstName')} error={errors.firstName?.message}><input className="input" {...register('firstName')} /></Field>
            <Field label={t('intake.fields.lastName')} error={errors.lastName?.message}><input className="input" {...register('lastName')} /></Field>
            <Field label={t('intake.fields.dateOfBirth')} error={errors.dateOfBirth?.message}><input className="input" type="date" {...register('dateOfBirth')} /></Field>
            <Field label={t('intake.fields.gender')} error={errors.gender?.message}>
              <select className="input" defaultValue="" {...register('gender')}>
                <option value="" disabled>{t('common.select')}</option>
                {(['Male', 'Female', 'NonBinary', 'PreferNotToSay'] as const).map(g => <option key={g} value={g}>{displayEnum(g)}</option>)}
              </select>
            </Field>
            <Field label={t('intake.fields.phone')} error={errors.phone?.message}><input className="input" type="tel" {...register('phone')} /></Field>
            <Field label={t('intake.fields.address')} error={errors.address?.message}><input className="input" {...register('address')} /></Field>
            <Field label={t('intake.fields.city')} error={errors.city?.message}><input className="input" {...register('city')} /></Field>
            <Field label={t('intake.fields.state')} error={errors.state?.message}><input className="input" {...register('state')} /></Field>
            <Field label={t('intake.fields.zipCode')} error={errors.zipCode?.message}><input className="input" {...register('zipCode')} /></Field>
            <Field label={t('intake.fields.insuranceProvider')} error={errors.insuranceProvider?.message}><input className="input" {...register('insuranceProvider')} /></Field>
            <Field label={t('intake.fields.insurancePolicyNumber')} error={errors.insurancePolicyNumber?.message}><input className="input" {...register('insurancePolicyNumber')} /></Field>
          </div>
        </section>

        <section className="space-y-4">
          <h2 className="font-semibold text-gray-900">{t('intake.public.history')}</h2>
          <Field label={t('intake.public.mainCondition')} error={errors.primaryCondition?.message}><input className="input" {...register('primaryCondition')} /></Field>
          <Field label={t('intake.fields.allergies')} error={errors.allergies?.message}><textarea className="input" rows={2} {...register('allergies')} /></Field>
          <Field label={t('intake.fields.currentMedications')} error={errors.currentMedications?.message}><textarea className="input" rows={3} {...register('currentMedications')} /></Field>
          <Field label={t('intake.public.pastHistory')} error={errors.pastHistory?.message}><textarea className="input" rows={3} {...register('pastHistory')} /></Field>
          <Field label={t('intake.public.anythingElse')} error={errors.additionalNotes?.message}><textarea className="input" rows={2} {...register('additionalNotes')} /></Field>
        </section>

        <section className="space-y-4">
          <h2 className="font-semibold text-gray-900">{t('intake.public.consent')}</h2>
          <p className="text-sm text-gray-700 bg-gray-50 rounded-lg p-4">{info.data.consentText}</p>
          <label className="flex items-start gap-2 text-sm text-gray-700">
            <input type="checkbox" className="mt-1" {...register('consentAgreed')} />
            <span>{t('intake.public.agree')}</span>
          </label>
          {errors.consentAgreed && <p className="text-red-500 text-xs">{tError(t, errors.consentAgreed.message ?? '')}</p>}
          <Field label={t('intake.public.signature')} error={errors.signatureName?.message}>
            <input className="input" autoComplete="off" {...register('signatureName')} />
          </Field>
        </section>

        {linkGone && <p className="text-sm text-red-600">{t('intake.public.invalidLinkAskNew')}</p>}
        {limited && <p className="text-sm text-red-600">{t('errors.tooManyRequests')}</p>}
        {serverErrors.length > 0 && <ul className="text-sm text-red-600 list-disc pl-5">{serverErrors.map(e => <li key={e}>{e}</li>)}</ul>}

        <button type="submit" className="btn-primary w-full h-10" disabled={submit.isPending}>
          {submit.isPending ? <Spinner className="w-4 h-4" /> : t('intake.public.submit')}
        </button>
      </form>
    </Shell>
  )
}

// ── Doctor: review list ──────────────────────────────────────────────────────
const FILTERS: IntakeStatus[] = ['Pending', 'Accepted', 'Rejected']

export function IntakeListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [status, setStatus] = useState<IntakeStatus>('Pending')
  const [page, setPage] = useState(1)
  const { data, isLoading } = useIntakeSubmissions(status, page)

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={t('intake.title')} subtitle={t('intake.subtitle')} />
      <div className="flex-1 overflow-auto px-8 py-6 space-y-4">
        <div className="flex gap-2">
          {FILTERS.map(f => (
            <button key={f} className={status === f ? 'btn-primary text-xs' : 'btn-secondary text-xs'}
              onClick={() => { setStatus(f); setPage(1) }}>{displayEnum(f)}</button>
          ))}
        </div>
        {isLoading ? <PageSpinner /> : !data?.items.length ? (
          <EmptyState title={t(`intake.empty.${status}`)} description={t('intake.emptyHint')} />
        ) : (
          <div className="card overflow-hidden">
            <table className="w-full text-sm">
              <thead><tr className="text-left text-gray-500 border-b border-border">
                <th className="px-4 py-3">{t('intake.cols.patient')}</th><th className="px-4 py-3">{t('intake.cols.submitted')}</th><th className="px-4 py-3">{t('intake.cols.status')}</th>
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
const ROWS: (keyof IntakeAnswers)[] = [
  'firstName', 'lastName', 'dateOfBirth', 'gender', 'phone', 'address', 'city', 'state', 'zipCode',
  'insuranceProvider', 'insurancePolicyNumber', 'primaryCondition', 'allergies',
  'currentMedications', 'pastHistory', 'additionalNotes',
]

export function IntakeReviewPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const submissionId = Number(id)
  const navigate = useNavigate()
  const { data, isLoading } = useIntakeSubmission(submissionId)
  const decide = useDecideIntake(submissionId)
  const [reason, setReason] = useState('')

  if (isLoading) return <PageSpinner />
  if (!data) return <div className="p-8 text-gray-500">{t('intake.review.notFound')}</div>
  const pending = data.status === 'Pending'
  const show = (v?: string | null) => (v && v.trim() ? v : '—')

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={t('intake.review.title')} subtitle={t('intake.review.submitted', { date: fmt.date(data.submittedAt) })}>
        <button className="btn-ghost gap-1" onClick={() => navigate('/intake')}><ArrowLeft size={15} /> {t('common.back')}</button>
      </PageHeader>
      <div className="flex-1 overflow-auto px-8 py-6 space-y-5">
        <div className="card p-5">
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-semibold text-gray-900">{t('intake.review.compared')}</h2>
            <Badge status={data.status} />
          </div>
          <table className="w-full text-sm">
            <thead><tr className="text-left text-gray-500"><th className="py-2 w-1/4">{t('intake.review.field')}</th><th>{t('intake.review.answer')}</th><th>{t('intake.review.current')}</th></tr></thead>
            <tbody>
              {ROWS.map(key => {
                const a = data.answers[key] as string | null | undefined
                const c = data.current?.[key] as string | null | undefined
                const changed = (a ?? '') !== (c ?? '')
                return (
                  <tr key={key} className="border-t border-border">
                    <td className="py-2 text-gray-500">{t(`intake.fields.${key}`)}</td>
                    <td className={`py-2 whitespace-pre-wrap ${changed ? 'font-medium text-gray-900' : 'text-gray-600'}`}>{show(a)}</td>
                    <td className="py-2 text-gray-500 whitespace-pre-wrap">
                      {key === 'currentMedications' || key === 'pastHistory' || key === 'additionalNotes' ? t('intake.review.addedToNotes') : show(c)}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>

        <div className="card p-5 text-sm space-y-1">
          <h2 className="font-semibold text-gray-900 mb-2">{t('intake.public.consent')}</h2>
          <p><span className="text-gray-500">{t('intake.review.signedAs')}</span> {data.consent.signatureName}</p>
          <p><span className="text-gray-500">{t('intake.review.signedAt')}</span> {fmt.timestamp(data.consent.signedAt)}</p>
          <p><span className="text-gray-500">{t('intake.review.version')}</span> {data.consent.consentVersion}</p>
          <p><span className="text-gray-500">{t('intake.review.agreement')}</span> {data.consent.consentAgreed ? t('intake.review.agreed') : t('intake.review.notAgreed')}</p>
        </div>

        {pending ? (
          <div className="card p-5 space-y-3">
            <p className="text-sm text-gray-600">{t('intake.review.decisionHelp')}</p>
            <input className="input" placeholder={t('intake.review.reasonPlaceholder')} maxLength={500}
              value={reason} onChange={e => setReason(e.target.value)} />
            <div className="flex gap-2">
              <button className="btn-primary" disabled={decide.isPending} onClick={() => decide.mutate({ accept: true })}>{t('intake.review.accept')}</button>
              <button className="btn-secondary" disabled={decide.isPending} onClick={() => decide.mutate({ accept: false, reason })}>{t('intake.review.reject')}</button>
            </div>
          </div>
        ) : (
          <p className="text-sm text-gray-500">
            {displayEnum(data.status)} {data.decidedAt ? fmt.date(data.decidedAt) : ''}{data.rejectionReason ? ` — ${data.rejectionReason}` : ''}
          </p>
        )}
      </div>
    </div>
  )
}
