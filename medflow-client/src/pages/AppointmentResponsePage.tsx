import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery } from '@tanstack/react-query'
import { appointmentResponseApi } from '@/api/services'
import { AuthShell } from '@/pages/AuthPages'
import { PageSpinner, Spinner } from '@/components/ui'
import { fmt, displayEnum } from '@/utils/format'
import type { ReminderAction } from '@/types'

// Public page opened from the reminder email. Viewing never changes anything; the patient must choose.
export function AppointmentResponsePage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''

  const lookup = useQuery({
    queryKey: ['appointment-response', token],
    queryFn: () => appointmentResponseApi.lookup(token),
    enabled: !!token,
    retry: false,
  })
  const respond = useMutation({
    mutationFn: (action: ReminderAction) => appointmentResponseApi.respond(token, action),
    onSuccess: () => lookup.refetch(),
  })

  const closedError = (respond.error as any)?.response?.status === 409
  const invalid = !token || lookup.isError

  return (
    <AuthShell title={t('response.title')} subtitle={t('response.subtitle')}>
      {!token || invalid ? (
        <p className="text-sm text-gray-700 text-center">
          {closedError ? t('response.closed') : t('response.invalid')}
        </p>
      ) : lookup.isLoading || !lookup.data ? <PageSpinner /> : (
        <div className="space-y-4">
          <div className="text-sm text-gray-700 space-y-1">
            <p><strong>{fmt.date(lookup.data.appointmentAt)}</strong> {t('response.at')} <strong>{fmt.time(lookup.data.appointmentAt)}</strong></p>
            <p>{t('response.with', { doctor: lookup.data.doctorName, minutes: lookup.data.durationMinutes })}</p>
            {lookup.data.location && <p>{t('response.location', { location: lookup.data.location })}</p>}
            <p>{t('response.status')} <strong>{displayEnum(lookup.data.status)}</strong></p>
          </div>
          {closedError && <p className="text-sm text-red-600">{t('response.closed')}</p>}
          {lookup.data.canRespond ? (
            <div className="flex gap-3">
              <button className="btn-primary flex-1 h-10" disabled={respond.isPending || lookup.data.status === 'Confirmed'}
                onClick={() => respond.mutate('Confirm')}>
                {respond.isPending ? <Spinner className="w-4 h-4" /> : t('response.confirm')}
              </button>
              <button className="btn-danger flex-1 h-10" disabled={respond.isPending}
                onClick={() => respond.mutate('Cancel')}>{t('response.cancel')}</button>
            </div>
          ) : (
            <p className="text-sm text-gray-500">{t('response.closed')}</p>
          )}
        </div>
      )}
    </AuthShell>
  )
}
