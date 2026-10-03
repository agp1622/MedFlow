import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { appointmentResponseApi } from '@/api/services'
import { AuthShell } from '@/pages/AuthPages'
import { PageSpinner, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import type { ReminderAction } from '@/types'

// Public page opened from the reminder email. Viewing never changes anything; the patient must choose.
export function AppointmentResponsePage() {
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

  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here; narrowing would change call signatures
  const closedError = (respond.error as any)?.response?.status === 409
  const invalid = !token || lookup.isError

  return (
    <AuthShell title="Your appointment" subtitle="Confirm or cancel your visit">
      {!token || invalid ? (
        <p className="text-sm text-gray-700 text-center">
          {closedError ? 'This appointment can no longer be changed.' : 'This link is not valid.'}
        </p>
      ) : lookup.isLoading || !lookup.data ? <PageSpinner /> : (
        <div className="space-y-4">
          <div className="text-sm text-gray-700 space-y-1">
            <p><strong>{fmt.date(lookup.data.appointmentAt)}</strong> at <strong>{fmt.time(lookup.data.appointmentAt)}</strong></p>
            <p>With {lookup.data.doctorName} ({lookup.data.durationMinutes} min)</p>
            {lookup.data.location && <p>Location: {lookup.data.location}</p>}
            <p>Status: <strong>{lookup.data.status}</strong></p>
          </div>
          {closedError && <p className="text-sm text-red-600">This appointment can no longer be changed.</p>}
          {lookup.data.canRespond ? (
            <div className="flex gap-3">
              <button className="btn-primary flex-1 h-10" disabled={respond.isPending || lookup.data.status === 'Confirmed'}
                onClick={() => respond.mutate('Confirm')}>
                {respond.isPending ? <Spinner className="w-4 h-4" /> : 'Confirm'}
              </button>
              <button className="btn-danger flex-1 h-10" disabled={respond.isPending}
                onClick={() => respond.mutate('Cancel')}>Cancel appointment</button>
            </div>
          ) : (
            <p className="text-sm text-gray-500">This appointment can no longer be changed.</p>
          )}
        </div>
      )}
    </AuthShell>
  )
}
