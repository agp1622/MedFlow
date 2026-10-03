import { useNavigate, Link, useSearchParams } from 'react-router-dom'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { tError } from '@/i18n'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation } from '@tanstack/react-query'
import { authApi } from '@/api/services'
import { useAuthStore } from '@/store/authStore'
import { Spinner, PasswordInput } from '@/components/ui'
import { LanguageSwitcher } from '@/components/ui/LanguageSwitcher'
import toast from 'react-hot-toast'
import { GoogleLogin } from '@react-oauth/google'

// ── Login ─────────────────────────────────────────────────────────────────────
const loginSchema = z.object({
  email: z.string().email('validation.invalidEmail'),
  password: z.string().min(1, 'validation.required'),
})
type LoginForm = z.infer<typeof loginSchema>

type Audience = 'doctor' | 'patient'

export function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const login = useAuthStore(s => s.login)
  const [audience, setAudience] = useState<Audience>('doctor')
  const { register, handleSubmit, formState: { errors } } = useForm<LoginForm>({ resolver: zodResolver(loginSchema) })
  const isPatient = audience === 'patient'

  const mutation = useMutation({
    mutationFn: authApi.login,
    onSuccess: (data) => {
      // Each tab only signs in its own kind of account
      if ((data.user.role === 'Patient') !== isPatient) {
        toast.error(isPatient ? t('auth.login.doctorAccountHint') : t('auth.login.patientAccountHint'))
        setAudience(isPatient ? 'doctor' : 'patient')
        return
      }
      login(data.token, data.user)
      navigate('/')
    },
    onError: () => toast.error(t('auth.login.invalidCredentials')),
  })

  const googleMutation = useMutation({
    mutationFn: authApi.googleLogin,
    onSuccess: (data) => { login(data.token, data.user); navigate('/') },
    onError: (err: any) => {
      const data = err?.response?.data
      if (data?.code === 'patient_account') setAudience('patient')
      toast.error(data?.error ?? t('auth.googleFailed'))
    },
  })

  return (
    <AuthShell
      title={isPatient ? t('auth.login.patientTitle') : t('auth.login.title')}
      subtitle={isPatient ? t('auth.login.patientSubtitle') : t('auth.login.subtitle')}>
      <div role="tablist" aria-label={t('auth.login.accountType')} className="grid grid-cols-2 gap-1 p-1 mb-6 rounded-xl bg-gray-100">
        {(['doctor', 'patient'] as const).map(a => (
          <button key={a} type="button" role="tab" aria-selected={audience === a}
            onClick={() => setAudience(a)}
            className={`py-2 rounded-lg text-sm font-semibold transition-all ${
              audience === a ? 'bg-white text-primary-600 shadow-sm' : 'text-gray-500 hover:text-gray-700'
            }`}>
            {a === 'doctor' ? t('auth.login.imDoctor') : t('auth.login.imPatient')}
          </button>
        ))}
      </div>

      <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
        <Field label={t('auth.email')} error={errors.email?.message}>
          <input className="input" type="email" placeholder={isPatient ? t('auth.emailPlaceholderPatient') : t('auth.emailPlaceholderDoctor')} {...register('email')} />
        </Field>
        <Field label={t('auth.password')} error={errors.password?.message}>
          <PasswordInput placeholder="••••••••" registration={register('password')} />
        </Field>
        <div className="text-right -mt-2">
          <Link to="/forgot-password" className="text-primary-600 text-sm font-semibold hover:underline">{t('auth.login.forgot')}</Link>
        </div>
        <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending || googleMutation.isPending}>
          {mutation.isPending ? <Spinner className="w-4 h-4" /> : t('auth.login.submit')}
        </button>
      </form>

      {isPatient ? (
        <p className="text-center text-sm text-gray-500 mt-5">
          {t('auth.login.patientInviteNote')}
        </p>
      ) : (
        <>
          <div className="mt-6 mb-4 flex items-center justify-center">
            <div className="w-full h-px bg-gray-200"></div>
            <span className="px-4 text-sm text-gray-500 bg-surface">{t('auth.or')}</span>
            <div className="w-full h-px bg-gray-200"></div>
          </div>

          <div className="flex justify-center">
            <GoogleLogin
              onSuccess={credentialResponse => {
                if (credentialResponse.credential) {
                  googleMutation.mutate({ credential: credentialResponse.credential })
                }
              }}
              onError={() => toast.error(t('auth.googleFailed'))}
            />
          </div>

          <p className="text-center text-sm text-gray-500 mt-5">
            {t('auth.login.noAccount')} <Link to="/register" className="text-primary-600 font-semibold hover:underline">{t('auth.login.register')}</Link>
          </p>
        </>
      )}
    </AuthShell>
  )
}

// ── Register ──────────────────────────────────────────────────────────────────
const registerSchema = z.object({
  firstName: z.string().min(1, 'validation.required'),
  lastName: z.string().min(1, 'validation.required'),
  email: z.string().email('validation.invalidEmail'),
  specialty: z.string().min(1, 'validation.required'),
  password: z.string().min(8, 'validation.passwordMin').regex(/[A-Z]/, 'validation.passwordUpper'),
  confirmPassword: z.string(),
}).refine(d => d.password === d.confirmPassword, { message: 'validation.passwordMismatch', path: ['confirmPassword'] })
type RegisterForm = z.infer<typeof registerSchema>

export function RegisterPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const login = useAuthStore(s => s.login)
  const { register, handleSubmit, formState: { errors } } = useForm<RegisterForm>({ resolver: zodResolver(registerSchema) })

  const mutation = useMutation({
    mutationFn: ({ confirmPassword, ...rest }: RegisterForm) => authApi.register(rest),
    onSuccess: (data) => { login(data.token, data.user); navigate('/') },
    onError: () => toast.error(t('auth.register.failed')),
  })

  const googleMutation = useMutation({
    mutationFn: authApi.googleLogin,
    onSuccess: (data) => { login(data.token, data.user); navigate('/') },
    onError: () => toast.error(t('auth.googleFailed')),
  })

  return (
    <AuthShell title={t('auth.register.title')} subtitle={t('auth.register.subtitle')}>
      <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
           <Field label={t('auth.register.firstName')} error={errors.firstName?.message}>
            <input className="input" placeholder={t('auth.register.firstNamePlaceholder')} {...register('firstName')} />
          </Field>
          <Field label={t('auth.register.lastName')} error={errors.lastName?.message}>
            <input className="input" placeholder={t('auth.register.lastNamePlaceholder')} {...register('lastName')} />
          </Field>
        </div>
         <Field label={t('auth.email')} error={errors.email?.message}>
          <input className="input" type="email" placeholder={t('auth.emailPlaceholderDoctor')} {...register('email')} />
        </Field>
        <Field label={t('auth.register.specialty')} error={errors.specialty?.message}>
          <input className="input" placeholder={t('auth.register.specialtyPlaceholder')} {...register('specialty')} />
        </Field>
        <Field label={t('auth.password')} error={errors.password?.message}>
          <PasswordInput placeholder={t('auth.register.passwordPlaceholder')} registration={register('password')} />
        </Field>
        <Field label={t('auth.confirmPassword')} error={errors.confirmPassword?.message}>
          <PasswordInput placeholder={t('auth.repeatPassword')} registration={register('confirmPassword')} />
        </Field>
        <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending || googleMutation.isPending}>
          {mutation.isPending ? <Spinner className="w-4 h-4" /> : t('auth.register.submit')}
        </button>
      </form>

      <div className="mt-6 mb-4 flex items-center justify-center">
        <div className="w-full h-px bg-gray-200"></div>
        <span className="px-4 text-sm text-gray-500 bg-surface">{t('auth.or')}</span>
        <div className="w-full h-px bg-gray-200"></div>
      </div>
      
      <div className="flex justify-center">
        <GoogleLogin
          onSuccess={credentialResponse => {
            if (credentialResponse.credential) {
              googleMutation.mutate({ credential: credentialResponse.credential })
            }
          }}
          onError={() => toast.error(t('auth.googleFailed'))}
        />
      </div>

      <p className="text-center text-sm text-gray-500 mt-5">
        {t('auth.register.haveAccount')} <Link to="/login" className="text-primary-600 font-semibold hover:underline">{t('auth.register.signIn')}</Link>
      </p>
    </AuthShell>
  )
}

// ── Forgot Password ───────────────────────────────────────────────────────────
const forgotSchema = z.object({ email: z.string().email('validation.invalidEmail') })
type ForgotForm = z.infer<typeof forgotSchema>

export function ForgotPasswordPage() {
  const { t } = useTranslation()
  const [message, setMessage] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors } } = useForm<ForgotForm>({ resolver: zodResolver(forgotSchema) })

  const mutation = useMutation({
    mutationFn: authApi.forgotPassword,
    onSuccess: (data) => setMessage(data.message),
    onError: (err: any) => toast.error(
      err?.response?.status === 429 ? t('errors.tooManyRequests') : t('errors.generic')),
  })

  return (
    <AuthShell title={t('auth.forgot.title')} subtitle={t('auth.forgot.subtitle')}>
      {message ? (
        <div className="space-y-4">
          <p className="text-sm text-gray-700">{message}</p>
          <p className="text-center text-sm text-gray-500">
            <Link to="/login" className="text-primary-600 font-semibold hover:underline">{t('auth.backToSignIn')}</Link>
          </p>
        </div>
      ) : (
        <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
          <Field label={t('auth.email')} error={errors.email?.message}>
            <input className="input" type="email" placeholder={t('auth.emailPlaceholderDoctor')} {...register('email')} />
          </Field>
          <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending}>
            {mutation.isPending ? <Spinner className="w-4 h-4" /> : t('auth.forgot.submit')}
          </button>
          <p className="text-center text-sm text-gray-500">
            <Link to="/login" className="text-primary-600 font-semibold hover:underline">{t('auth.backToSignIn')}</Link>
          </p>
        </form>
      )}
    </AuthShell>
  )
}

// ── Reset Password ────────────────────────────────────────────────────────────
const resetSchema = z.object({
  newPassword: z.string().min(8, 'validation.passwordMin').regex(/[A-Z]/, 'validation.passwordUpper').regex(/[0-9]/, 'validation.passwordDigit'),
  confirmPassword: z.string(),
}).refine(d => d.newPassword === d.confirmPassword, { message: 'validation.passwordMismatch', path: ['confirmPassword'] })
type ResetForm = z.infer<typeof resetSchema>

export function ResetPasswordPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const email = params.get('email') ?? ''
  const [done, setDone] = useState(false)
  const [linkError, setLinkError] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors } } = useForm<ResetForm>({ resolver: zodResolver(resetSchema) })

  const mutation = useMutation({
    mutationFn: (d: ResetForm) => authApi.resetPassword({ email, token, ...d }),
    onSuccess: () => setDone(true),
    onError: (err: any) => {
      const data = err?.response?.data
      if (data?.error) setLinkError(data.error)
      else toast.error(data?.errors?.[0] ?? t('auth.reset.failed'))
    },
  })

  const invalid = !token || !email || linkError

  return (
    <AuthShell title={t('auth.reset.title')} subtitle={t('auth.reset.subtitle')}>
      {done ? (
        <div className="space-y-4 text-center">
          <p className="text-sm text-gray-700">{t('auth.reset.done')}</p>
          <Link to="/login" className="btn-primary w-full h-10 inline-flex items-center justify-center">{t('auth.goToSignIn')}</Link>
        </div>
      ) : invalid ? (
        <div className="space-y-4 text-center">
          <p className="text-sm text-gray-700">{linkError ?? t('auth.reset.invalidLink')}</p>
          <Link to="/forgot-password" className="btn-primary w-full h-10 inline-flex items-center justify-center">{t('auth.reset.requestNew')}</Link>
        </div>
      ) : (
        <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
          <Field label={t('auth.reset.newPassword')} error={errors.newPassword?.message}>
            <input className="input" type="password" placeholder={t('auth.strongPasswordPlaceholder')} {...register('newPassword')} />
          </Field>
          <Field label={t('auth.reset.confirmNew')} error={errors.confirmPassword?.message}>
            <input className="input" type="password" placeholder={t('auth.repeatPassword')} {...register('confirmPassword')} />
          </Field>
          <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending}>
            {mutation.isPending ? <Spinner className="w-4 h-4" /> : t('auth.reset.submit')}
          </button>
        </form>
      )}
    </AuthShell>
  )
}

// ── Accept portal invitation ──────────────────────────────────────────────────
export function AcceptInvitePage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const login = useAuthStore(s => s.login)
  const token = params.get('token') ?? ''
  const email = params.get('email') ?? ''
  const [linkError, setLinkError] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors } } = useForm<ResetForm>({ resolver: zodResolver(resetSchema) })

  const mutation = useMutation({
    mutationFn: (d: ResetForm) => authApi.acceptInvitation({ token, email, password: d.newPassword, confirmPassword: d.confirmPassword }),
    onSuccess: (data) => { login(data.token, data.user); navigate('/portal') },
    onError: (err: any) => {
      const data = err?.response?.data
      if (data?.error) setLinkError(data.error)
      else toast.error(data?.errors?.[0] ?? t('auth.invite.failed'))
    },
  })

  const invalid = !token || !email || linkError

  return (
    <AuthShell title={t('auth.invite.title')} subtitle={t('auth.invite.subtitle')}>
      {invalid ? (
        <div className="space-y-4 text-center">
          <p className="text-sm text-gray-700">{linkError ?? t('auth.invite.invalid')}</p>
          <p className="text-sm text-gray-500">{t('auth.invite.askNew')}</p>
          <Link to="/login" className="btn-primary w-full h-10 inline-flex items-center justify-center">{t('auth.goToSignIn')}</Link>
        </div>
      ) : (
        <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
          <p className="text-sm text-gray-600">{t('auth.invite.settingUp')} <strong>{email}</strong></p>
          <Field label={t('auth.password')} error={errors.newPassword?.message}>
            <PasswordInput placeholder={t('auth.strongPasswordPlaceholder')} registration={register('newPassword')} />
          </Field>
          <Field label={t('auth.confirmPassword')} error={errors.confirmPassword?.message}>
            <PasswordInput placeholder={t('auth.repeatPassword')} registration={register('confirmPassword')} />
          </Field>
          <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending}>
            {mutation.isPending ? <Spinner className="w-4 h-4" /> : t('auth.invite.submit')}
          </button>
        </form>
      )}
    </AuthShell>
  )
}

// ── Shared Auth Shell ─────────────────────────────────────────────────────────
export function AuthShell({ title, subtitle, children }: { title: string; subtitle: string; children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-surface flex items-center justify-center px-4 relative">
      <LanguageSwitcher className="absolute top-3 right-3" />
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-4">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center text-white font-bold">✚</div>
            <span className="text-2xl font-bold text-gray-900">MedFlow</span>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">{title}</h1>
          <p className="text-gray-500 text-sm mt-1">{subtitle}</p>
        </div>
        <div className="card p-5 sm:p-8">{children}</div>
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
