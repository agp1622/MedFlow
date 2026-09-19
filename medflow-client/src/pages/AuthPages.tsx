import { useNavigate, Link, useSearchParams } from 'react-router-dom'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation } from '@tanstack/react-query'
import { authApi } from '@/api/services'
import { useAuthStore } from '@/store/authStore'
import { Spinner } from '@/components/ui'
import toast from 'react-hot-toast'
import { GoogleLogin } from '@react-oauth/google'

// ── Login ─────────────────────────────────────────────────────────────────────
const loginSchema = z.object({
  email: z.string().email('Invalid email'),
  password: z.string().min(1, 'Required'),
})
type LoginForm = z.infer<typeof loginSchema>

export function LoginPage() {
  const navigate = useNavigate()
  const login = useAuthStore(s => s.login)
  const { register, handleSubmit, formState: { errors } } = useForm<LoginForm>({ resolver: zodResolver(loginSchema) })

  const mutation = useMutation({
    mutationFn: authApi.login,
    onSuccess: (data) => { login(data.token, data.user); navigate('/') },
    onError: () => toast.error('Invalid email or password'),
  })

  const googleMutation = useMutation({
    mutationFn: authApi.googleLogin,
    onSuccess: (data) => { login(data.token, data.user); navigate('/') },
    onError: () => toast.error('Google sign in failed'),
  })

  return (
    <AuthShell title="Welcome back" subtitle="Sign in to your MedFlow account">
      <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
        <Field label="Email" error={errors.email?.message}>
          <input className="input" type="email" placeholder="doctor@clinic.com" {...register('email')} />
        </Field>
        <Field label="Password" error={errors.password?.message}>
          <input className="input" type="password" placeholder="••••••••" {...register('password')} />
        </Field>
        <div className="text-right -mt-2">
          <Link to="/forgot-password" className="text-primary-600 text-sm font-semibold hover:underline">Forgot password?</Link>
        </div>
        <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending || googleMutation.isPending}>
          {mutation.isPending ? <Spinner className="w-4 h-4" /> : 'Sign In'}
        </button>
      </form>

      <div className="mt-6 mb-4 flex items-center justify-center">
        <div className="w-full h-px bg-gray-200"></div>
        <span className="px-4 text-sm text-gray-500 bg-surface">OR</span>
        <div className="w-full h-px bg-gray-200"></div>
      </div>
      
      <div className="flex justify-center">
        <GoogleLogin
          onSuccess={credentialResponse => {
            if (credentialResponse.credential) {
              googleMutation.mutate({ credential: credentialResponse.credential })
            }
          }}
          onError={() => toast.error('Google Sign-In failed')}
        />
      </div>

      <p className="text-center text-sm text-gray-500 mt-5">
        No account? <Link to="/register" className="text-primary-600 font-semibold hover:underline">Register</Link>
      </p>
    </AuthShell>
  )
}

// ── Register ──────────────────────────────────────────────────────────────────
const registerSchema = z.object({
  firstName: z.string().min(1, 'Required'),
  lastName: z.string().min(1, 'Required'),
  email: z.string().email('Invalid email'),
  specialty: z.string().min(1, 'Required'),
  password: z.string().min(8, 'Minimum 8 characters').regex(/[A-Z]/, 'Must contain uppercase'),
  confirmPassword: z.string(),
}).refine(d => d.password === d.confirmPassword, { message: 'Passwords do not match', path: ['confirmPassword'] })
type RegisterForm = z.infer<typeof registerSchema>

export function RegisterPage() {
  const navigate = useNavigate()
  const login = useAuthStore(s => s.login)
  const { register, handleSubmit, formState: { errors } } = useForm<RegisterForm>({ resolver: zodResolver(registerSchema) })

  const mutation = useMutation({
    mutationFn: ({ confirmPassword, ...rest }: RegisterForm) => authApi.register(rest),
    onSuccess: (data) => { login(data.token, data.user); navigate('/') },
    onError: () => toast.error('Registration failed. Email may already be in use.'),
  })

  const googleMutation = useMutation({
    mutationFn: authApi.googleLogin,
    onSuccess: (data) => { login(data.token, data.user); navigate('/') },
    onError: () => toast.error('Google sign in failed'),
  })

  return (
    <AuthShell title="Create your account" subtitle="Start managing your patients today">
      <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <Field label="First name" error={errors.firstName?.message}>
            <input className="input" placeholder="John" {...register('firstName')} />
          </Field>
          <Field label="Last name" error={errors.lastName?.message}>
            <input className="input" placeholder="Smith" {...register('lastName')} />
          </Field>
        </div>
        <Field label="Email" error={errors.email?.message}>
          <input className="input" type="email" placeholder="doctor@clinic.com" {...register('email')} />
        </Field>
        <Field label="Specialty" error={errors.specialty?.message}>
          <input className="input" placeholder="e.g. Cardiology" {...register('specialty')} />
        </Field>
        <Field label="Password" error={errors.password?.message}>
          <input className="input" type="password" placeholder="Min 8 chars, 1 uppercase" {...register('password')} />
        </Field>
        <Field label="Confirm password" error={errors.confirmPassword?.message}>
          <input className="input" type="password" placeholder="Repeat password" {...register('confirmPassword')} />
        </Field>
        <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending || googleMutation.isPending}>
          {mutation.isPending ? <Spinner className="w-4 h-4" /> : 'Create Account'}
        </button>
      </form>

      <div className="mt-6 mb-4 flex items-center justify-center">
        <div className="w-full h-px bg-gray-200"></div>
        <span className="px-4 text-sm text-gray-500 bg-surface">OR</span>
        <div className="w-full h-px bg-gray-200"></div>
      </div>
      
      <div className="flex justify-center">
        <GoogleLogin
          onSuccess={credentialResponse => {
            if (credentialResponse.credential) {
              googleMutation.mutate({ credential: credentialResponse.credential })
            }
          }}
          onError={() => toast.error('Google Sign-In failed')}
        />
      </div>

      <p className="text-center text-sm text-gray-500 mt-5">
        Already have an account? <Link to="/login" className="text-primary-600 font-semibold hover:underline">Sign in</Link>
      </p>
    </AuthShell>
  )
}

// ── Forgot Password ───────────────────────────────────────────────────────────
const forgotSchema = z.object({ email: z.string().email('Invalid email') })
type ForgotForm = z.infer<typeof forgotSchema>

export function ForgotPasswordPage() {
  const [message, setMessage] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors } } = useForm<ForgotForm>({ resolver: zodResolver(forgotSchema) })

  const mutation = useMutation({
    mutationFn: authApi.forgotPassword,
    onSuccess: (data) => setMessage(data.message),
    onError: (err: any) => toast.error(
      err?.response?.status === 429 ? 'Too many requests. Please try again later.' : 'Something went wrong. Please try again.'),
  })

  return (
    <AuthShell title="Forgot your password?" subtitle="Enter your email and we'll send you a reset link">
      {message ? (
        <div className="space-y-4">
          <p className="text-sm text-gray-700">{message}</p>
          <p className="text-center text-sm text-gray-500">
            <Link to="/login" className="text-primary-600 font-semibold hover:underline">Back to sign in</Link>
          </p>
        </div>
      ) : (
        <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
          <Field label="Email" error={errors.email?.message}>
            <input className="input" type="email" placeholder="doctor@clinic.com" {...register('email')} />
          </Field>
          <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending}>
            {mutation.isPending ? <Spinner className="w-4 h-4" /> : 'Send reset link'}
          </button>
          <p className="text-center text-sm text-gray-500">
            <Link to="/login" className="text-primary-600 font-semibold hover:underline">Back to sign in</Link>
          </p>
        </form>
      )}
    </AuthShell>
  )
}

// ── Reset Password ────────────────────────────────────────────────────────────
const resetSchema = z.object({
  newPassword: z.string().min(8, 'Minimum 8 characters').regex(/[A-Z]/, 'Must contain uppercase').regex(/[0-9]/, 'Must contain a digit'),
  confirmPassword: z.string(),
}).refine(d => d.newPassword === d.confirmPassword, { message: 'Passwords do not match', path: ['confirmPassword'] })
type ResetForm = z.infer<typeof resetSchema>

export function ResetPasswordPage() {
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
      else toast.error(data?.errors?.[0] ?? 'Could not reset password. Please try again.')
    },
  })

  const invalid = !token || !email || linkError

  return (
    <AuthShell title="Reset your password" subtitle="Choose a new password for your account">
      {done ? (
        <div className="space-y-4 text-center">
          <p className="text-sm text-gray-700">Your password has been reset. You can now sign in.</p>
          <Link to="/login" className="btn-primary w-full h-10 inline-flex items-center justify-center">Go to sign in</Link>
        </div>
      ) : invalid ? (
        <div className="space-y-4 text-center">
          <p className="text-sm text-gray-700">{linkError ?? 'This reset link is invalid or has expired. Please request a new one.'}</p>
          <Link to="/forgot-password" className="btn-primary w-full h-10 inline-flex items-center justify-center">Request a new link</Link>
        </div>
      ) : (
        <form onSubmit={handleSubmit(d => mutation.mutate(d))} className="space-y-4">
          <Field label="New password" error={errors.newPassword?.message}>
            <input className="input" type="password" placeholder="Min 8 chars, 1 uppercase, 1 digit" {...register('newPassword')} />
          </Field>
          <Field label="Confirm new password" error={errors.confirmPassword?.message}>
            <input className="input" type="password" placeholder="Repeat password" {...register('confirmPassword')} />
          </Field>
          <button type="submit" className="btn-primary w-full h-10" disabled={mutation.isPending}>
            {mutation.isPending ? <Spinner className="w-4 h-4" /> : 'Reset password'}
          </button>
        </form>
      )}
    </AuthShell>
  )
}

// ── Shared Auth Shell ─────────────────────────────────────────────────────────
function AuthShell({ title, subtitle, children }: { title: string; subtitle: string; children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-surface flex items-center justify-center px-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-4">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center text-white font-bold">✚</div>
            <span className="text-2xl font-bold text-gray-900">MedFlow</span>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">{title}</h1>
          <p className="text-gray-500 text-sm mt-1">{subtitle}</p>
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
