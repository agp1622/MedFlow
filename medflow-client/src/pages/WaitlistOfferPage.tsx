import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery } from '@tanstack/react-query'
import { waitlistOfferApi } from '@/api/services'
import { AuthShell } from '@/pages/AuthPages'
import { PageSpinner, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'

// Public page opened from the waitlist offer email. Viewing never changes anything; the patient must choose.
export function WaitlistOfferPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''

  const lookup = useQuery({
    queryKey: ['waitlist-offer', token],
    queryFn: () => waitlistOfferApi.lookup(token),
    enabled: !!token,
    retry: false,
  })
  const claim = useMutation({ mutationFn: () => waitlistOfferApi.claim(token) })
  const leave = useMutation({ mutationFn: () => waitlistOfferApi.leave(token) })

  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here; narrowing would change call signatures
  const taken = (claim.error as any)?.response?.status === 409
  const busy = claim.isPending || leave.isPending

  return (
    <AuthShell title={t('waitlistOffer.title')} subtitle={t('waitlistOffer.subtitle')}>
      {claim.isSuccess ? (
        <p className="text-sm text-gray-700 text-center">{t('waitlistOffer.booked')}</p>
      ) : leave.isSuccess ? (
        <p className="text-sm text-gray-700 text-center">{t('waitlistOffer.left')}</p>
      ) : !token || lookup.isError ? (
        <p className="text-sm text-gray-700 text-center">{t('waitlistOffer.invalid')}</p>
      ) : lookup.isLoading || !lookup.data ? <PageSpinner /> : (
        <div className="space-y-4">
          <div className="text-sm text-gray-700 space-y-1">
            <p><strong>{fmt.date(lookup.data.slotStartsAt)}</strong> {t('waitlistOffer.at')} <strong>{fmt.time(lookup.data.slotStartsAt)}</strong></p>
            <p>{t('waitlistOffer.with', { doctor: lookup.data.doctorName, minutes: lookup.data.durationMinutes })}</p>
            <p className="text-gray-500">{t('waitlistOffer.until', { date: fmt.date(lookup.data.expiresAt), time: fmt.time(lookup.data.expiresAt) })}</p>
          </div>
          {taken && <p className="text-sm text-red-600">{t('waitlistOffer.taken')}</p>}
          <div className="flex gap-3">
            <button className="btn-primary flex-1 h-10" disabled={busy || taken} onClick={() => claim.mutate()}>
              {claim.isPending ? <Spinner className="w-4 h-4" /> : t('waitlistOffer.book')}
            </button>
            <button className="btn-secondary flex-1 h-10" disabled={busy} onClick={() => leave.mutate()}>
              {t('waitlistOffer.leave')}
            </button>
          </div>
        </div>
      )}
    </AuthShell>
  )
}
