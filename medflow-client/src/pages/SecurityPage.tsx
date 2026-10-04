import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import QRCode from 'qrcode'
import toast from 'react-hot-toast'
import { Copy, Download, ShieldCheck } from 'lucide-react'
import {
  useTwoFactorStatus, useTwoFactorSetup, useEnableTwoFactor, useRegenerateRecoveryCodes, useDisableTwoFactor,
} from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { PageSpinner, Spinner, PasswordInput } from '@/components/ui'
import type { TwoFactorSetup } from '@/types'

const confirmSchema = z.object({ code: z.string().trim().regex(/^\d{3}\s?\d{3}$/, 'security.codeInvalid') })
type ConfirmForm = z.infer<typeof confirmSchema>

const sensitiveSchema = z.object({
  password: z.string().optional(),
  code: z.string().trim().min(1, 'validation.required').max(32, 'validation.required'),
})
type SensitiveForm = z.infer<typeof sensitiveSchema>

/** Security settings of the signed-in staff member: authenticator app setup, recovery codes, disable. */
export function SecurityPage() {
  const { t } = useTranslation()
  const { data: status, isLoading } = useTwoFactorStatus()
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null)
  // Recovery codes live only in this component's state: they are shown once and never stored
  const [codes, setCodes] = useState<string[] | null>(null)
  const [action, setAction] = useState<'regenerate' | 'disable' | null>(null)

  const beginSetup = useTwoFactorSetup()
  const enable = useEnableTwoFactor()
  const regenerate = useRegenerateRecoveryCodes()
  const disable = useDisableTwoFactor()

  const confirmForm = useForm<ConfirmForm>({ resolver: zodResolver(confirmSchema) })
  const sensitiveForm = useForm<SensitiveForm>({ resolver: zodResolver(sensitiveSchema) })

  if (isLoading || !status) return <PageSpinner />

  const closeAction = () => { setAction(null); sensitiveForm.reset() }

  const submitSensitive = (v: SensitiveForm) => {
    const body = { password: v.password || undefined, code: v.code }
    if (action === 'regenerate') {
      regenerate.mutate(body, { onSuccess: r => { setCodes(r.recoveryCodes); closeAction() } })
    } else if (action === 'disable') {
      disable.mutate(body, { onSuccess: closeAction })
    }
  }

  return (
    <>
      <PageHeader title={t('security.title')} subtitle={t('security.subtitle')} />
      <div className="flex-1 overflow-y-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6 space-y-6">
        {codes && <RecoveryCodesPanel codes={codes} onDone={() => setCodes(null)} />}

        <section className="card p-5 space-y-4">
          <div className="flex items-start gap-3">
            <ShieldCheck className={status.enabled ? 'text-green-600' : 'text-gray-400'} size={22} />
            <div className="min-w-0">
              <h2 className="font-semibold text-gray-900">{t('security.twoFactorTitle')}</h2>
              <p className="text-sm text-gray-500 mt-0.5">
                {status.enabled ? t('security.statusOn') : t('security.statusOff')}
              </p>
            </div>
          </div>

          {!status.enabled && !setup && (
            <>
              <p className="text-sm text-gray-600">{t('security.intro')}</p>
              <button type="button" className="btn-primary" disabled={beginSetup.isPending}
                onClick={() => beginSetup.mutate(undefined, { onSuccess: setSetup })}>
                {beginSetup.isPending ? <Spinner className="w-4 h-4" /> : t('security.start')}
              </button>
            </>
          )}

          {!status.enabled && setup && (
            <div className="space-y-4">
              <ol className="list-decimal pl-5 text-sm text-gray-600 space-y-1">
                <li>{t('security.step1')}</li>
                <li>{t('security.step2')}</li>
                <li>{t('security.step3')}</li>
              </ol>
              <SetupQr setup={setup} />
              <form className="flex flex-wrap items-end gap-3"
                onSubmit={confirmForm.handleSubmit(v => enable.mutate(v.code, {
                  onSuccess: r => { setCodes(r.recoveryCodes); setSetup(null); confirmForm.reset() },
                }))}>
                <div className="min-w-[180px]">
                  <label className="label" htmlFor="tf-code">{t('security.codeLabel')}</label>
                  <input id="tf-code" className="input" inputMode="numeric" autoComplete="one-time-code" maxLength={7}
                    placeholder="123456" {...confirmForm.register('code')} />
                  {confirmForm.formState.errors.code && (
                    <p className="text-red-500 text-xs mt-1">{t('security.codeInvalid')}</p>
                  )}
                </div>
                <button type="submit" className="btn-primary" disabled={enable.isPending}>
                  {enable.isPending ? <Spinner className="w-4 h-4" /> : t('security.confirm')}
                </button>
                <button type="button" className="btn-secondary" onClick={() => { setSetup(null); confirmForm.reset() }}>
                  {t('common.cancel')}
                </button>
              </form>
            </div>
          )}

          {status.enabled && (
            <div className="space-y-4">
              <p className="text-sm text-gray-600">
                {t('security.remaining', { count: status.recoveryCodesRemaining })}
              </p>
              {status.recoveryCodesRemaining <= 3 && (
                <p role="alert" className="text-sm text-amber-700 bg-amber-50 rounded-lg px-3 py-2">{t('security.fewLeft')}</p>
              )}
              {!action && (
                <div className="flex flex-wrap gap-3">
                  <button type="button" className="btn-secondary" onClick={() => setAction('regenerate')}>
                    {t('security.regenerate')}
                  </button>
                  <button type="button" className="btn-secondary text-red-600" onClick={() => setAction('disable')}>
                    {t('security.disable')}
                  </button>
                </div>
              )}
              {action && (
                <form className="space-y-3 max-w-sm" onSubmit={sensitiveForm.handleSubmit(submitSensitive)}>
                  <p className="text-sm text-gray-600">
                    {action === 'disable' ? t('security.disableHelp') : t('security.regenerateHelp')}
                  </p>
                  <div>
                    <label className="label" htmlFor="tf-password">{t('auth.password')}</label>
                    <PasswordInput placeholder="••••••••" registration={sensitiveForm.register('password')} />
                    <p className="text-xs text-gray-400 mt-1">{t('security.googleNoPassword')}</p>
                  </div>
                  <div>
                    <label className="label" htmlFor="tf-current-code">{t('security.currentCodeLabel')}</label>
                    <input id="tf-current-code" className="input" autoComplete="one-time-code" maxLength={32}
                      {...sensitiveForm.register('code')} />
                    {sensitiveForm.formState.errors.code && (
                      <p className="text-red-500 text-xs mt-1">{t('validation.required')}</p>
                    )}
                  </div>
                  <div className="flex gap-3">
                    <button type="submit" className={action === 'disable' ? 'btn-primary bg-red-600' : 'btn-primary'}
                      disabled={regenerate.isPending || disable.isPending}>
                      {action === 'disable' ? t('security.disableConfirm') : t('security.regenerateConfirm')}
                    </button>
                    <button type="button" className="btn-secondary" onClick={closeAction}>{t('common.cancel')}</button>
                  </div>
                </form>
              )}
            </div>
          )}
        </section>
      </div>
    </>
  )
}

/** The QR is drawn in the browser from the otpauth URI: the secret never leaves this page. */
function SetupQr({ setup }: { setup: TwoFactorSetup }) {
  const { t } = useTranslation()
  const [src, setSrc] = useState<string | null>(null)
  useEffect(() => {
    let cancelled = false
    QRCode.toDataURL(setup.otpAuthUri, { width: 192, margin: 1, errorCorrectionLevel: 'M' })
      .then(url => { if (!cancelled) setSrc(url) })
      .catch(() => { if (!cancelled) setSrc(null) })
    return () => { cancelled = true }
  }, [setup.otpAuthUri])
  const grouped = setup.sharedKey.replace(/(.{4})/g, '$1 ').trim()
  return (
    <div className="flex flex-wrap items-center gap-5">
      <div className="w-48 h-48 bg-white rounded-lg border border-border flex items-center justify-center">
        {src ? <img src={src} width={192} height={192} alt={t('security.qrAlt')} /> : <Spinner className="w-6 h-6" />}
      </div>
      <div className="min-w-0">
        <p className="text-xs text-gray-500">{t('security.manualKey')}</p>
        <p className="font-mono text-sm break-all select-all" data-testid="manual-key">{grouped}</p>
      </div>
    </div>
  )
}

function RecoveryCodesPanel({ codes, onDone }: { codes: string[]; onDone: () => void }) {
  const { t } = useTranslation()
  const [saved, setSaved] = useState(false)
  const text = codes.join('\n')

  const copy = async () => {
    try { await navigator.clipboard.writeText(text); toast.success(t('security.copied')) }
    catch { toast.error(t('errors.generic')) }
  }
  const download = () => {
    const blob = new Blob([`MedFlow\n${t('security.fileHeader')}\n\n${text}\n`], { type: 'text/plain' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'medflow-recovery-codes.txt'
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <section className="card p-5 space-y-4 border-amber-300" aria-live="polite">
      <h2 className="font-semibold text-gray-900">{t('security.codesTitle')}</h2>
      <p className="text-sm text-amber-700">{t('security.codesWarning')}</p>
      <ul className="grid grid-cols-2 sm:grid-cols-5 gap-2 font-mono text-sm">
        {codes.map(c => <li key={c} className="bg-gray-50 rounded px-2 py-1 text-center select-all">{c}</li>)}
      </ul>
      <div className="flex flex-wrap gap-3">
        <button type="button" className="btn-secondary" onClick={copy}><Copy size={14} /> {t('security.copy')}</button>
        <button type="button" className="btn-secondary" onClick={download}><Download size={14} /> {t('security.download')}</button>
      </div>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" checked={saved} onChange={e => setSaved(e.target.checked)} /> {t('security.savedConfirm')}
      </label>
      <button type="button" className="btn-primary" disabled={!saved} onClick={onDone}>{t('security.done')}</button>
    </section>
  )
}
