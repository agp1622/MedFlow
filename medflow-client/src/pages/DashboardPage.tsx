import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useDashboard } from '@/hooks/queries'
import { useAuthStore } from '@/store/authStore'
import { PageHeader } from '@/components/layout/AppLayout'
import { StatCard, Avatar, Badge, PageSpinner } from '@/components/ui'
import { fmt, displayEnum } from '@/utils/format'

export function DashboardPage() {
  const { t } = useTranslation()
  const { data, isLoading } = useDashboard()
  const user = useAuthStore(s => s.user)
  const navigate = useNavigate()
  const today = fmt.date(new Date().toISOString())

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={t('dashboard.title')} subtitle={today} />

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6 space-y-6">
          {/* Greeting */}
          <p className="text-gray-500 text-sm">
            {t(`dashboard.greeting.${getTimeOfDay()}`)}, <span className="font-semibold text-gray-800">{t('layout.doctorPrefix')} {user?.lastName}</span>. {t('dashboard.overview')}
          </p>

          {/* Stats row */}
          <div className="flex gap-4 flex-wrap">
            <StatCard icon="👤" label={t('dashboard.totalPatients')} value={data?.totalPatients ?? 0}
              sub={t('dashboard.activeCount', { count: data?.activePatients ?? 0 })} />
            <StatCard icon="📅" label={t('dashboard.todayAppointments')} value={data?.todayAppointments ?? 0}
              sub={t('dashboard.upcomingCount', { count: data?.upcomingAppointments ?? 0 })} color="text-violet-600" />
            <StatCard icon="💊" label={t('dashboard.activePrescriptions')} value={data?.activePrescriptions ?? 0}
              sub={data?.expiringPrescriptions ? t('dashboard.expiringSoon', { count: data.expiringPrescriptions }) : undefined}
              color="text-amber-600" />
            <StatCard icon="💳" label={t('dashboard.pendingInvoices')} value={fmt.currency(data?.pendingInvoicesAmount)}
              sub={data?.overdueInvoices ? t('dashboard.overdue', { count: data.overdueInvoices }) : undefined}
              color="text-red-500" />
          </div>

          {/* Two column */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
            {/* Today's schedule */}
            <div className="card overflow-hidden">
              <div className="px-5 py-4 border-b border-border flex items-center justify-between">
                <h2 className="font-bold text-gray-800">{t('dashboard.todaySchedule')}</h2>
                <button className="text-xs text-primary-600 font-semibold hover:underline"
                  onClick={() => navigate('/appointments')}>{t('common.viewAll')}</button>
              </div>
              <div className="divide-y divide-border">
                {data?.todaySchedule.length === 0 && (
                  <p className="text-gray-400 text-sm text-center py-8">{t('dashboard.noAppointmentsToday')}</p>
                )}
                {data?.todaySchedule.map(a => (
                  <div key={a.id} className="flex items-center gap-3 px-5 py-3 hover:bg-gray-50">
                    <Avatar name={a.patientName} size="sm" />
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-sm text-gray-800 truncate">{a.patientName}</p>
                      <p className="text-xs text-gray-400">{displayEnum(a.type)} · {t('common.minutes', { count: a.durationMinutes })}</p>
                    </div>
                    <div className="text-right flex-shrink-0">
                      <p className="text-sm font-bold text-primary-600">{fmt.time(a.scheduledAt)}</p>
                      <Badge status={a.status} />
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Recent patients */}
            <div className="card overflow-hidden">
              <div className="px-5 py-4 border-b border-border flex items-center justify-between">
                <h2 className="font-bold text-gray-800">{t('dashboard.recentPatients')}</h2>
                <button className="text-xs text-primary-600 font-semibold hover:underline"
                  onClick={() => navigate('/patients')}>{t('common.viewAll')}</button>
              </div>
              <div className="divide-y divide-border">
                {data?.recentPatients.length === 0 && (
                  <p className="text-gray-400 text-sm text-center py-8">{t('dashboard.noPatients')}</p>
                )}
                {data?.recentPatients.map(p => (
                  <div key={p.id} className="flex items-center gap-3 px-5 py-3 hover:bg-gray-50 cursor-pointer"
                    onClick={() => navigate(`/patients/${p.id}`)}>
                    <Avatar name={p.fullName} size="sm" />
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-sm text-gray-800 truncate">{p.fullName}</p>
                      <p className="text-xs text-gray-400">{p.primaryCondition ?? t('dashboard.noCondition')} · {t('common.age', { count: p.age })}</p>
                    </div>
                    <Badge status={p.status} />
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function getTimeOfDay() {
  const h = new Date().getHours()
  if (h < 12) return 'morning'
  if (h < 17) return 'afternoon'
  return 'evening'
}
