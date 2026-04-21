import { useNavigate } from 'react-router-dom'
import { useDashboard } from '@/hooks/queries'
import { useAuthStore } from '@/store/authStore'
import { PageHeader } from '@/components/layout/AppLayout'
import { StatCard, Avatar, Badge, PageSpinner } from '@/components/ui'
import { fmt } from '@/utils/format'

export function DashboardPage() {
  const { data, isLoading } = useDashboard()
  const user = useAuthStore(s => s.user)
  const navigate = useNavigate()
  const today = fmt.date(new Date().toISOString())

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title="Dashboard" subtitle={today} />

      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-8 py-6 space-y-6">
          {/* Greeting */}
          <p className="text-gray-500 text-sm">
            Good {getTimeOfDay()}, <span className="font-semibold text-gray-800">Dr. {user?.lastName}</span>. Here's your overview.
          </p>

          {/* Stats row */}
          <div className="flex gap-4 flex-wrap">
            <StatCard icon="👤" label="Total Patients" value={data?.totalPatients ?? 0}
              sub={`${data?.activePatients ?? 0} active`} />
            <StatCard icon="📅" label="Today's Appointments" value={data?.todayAppointments ?? 0}
              sub={`${data?.upcomingAppointments ?? 0} upcoming`} color="text-violet-600" />
            <StatCard icon="💊" label="Active Prescriptions" value={data?.activePrescriptions ?? 0}
              sub={data?.expiringPrescriptions ? `${data.expiringPrescriptions} expiring soon` : undefined}
              color="text-amber-600" />
            <StatCard icon="💳" label="Pending Invoices" value={fmt.currency(data?.pendingInvoicesAmount)}
              sub={data?.overdueInvoices ? `${data.overdueInvoices} overdue` : undefined}
              color="text-red-500" />
          </div>

          {/* Two column */}
          <div className="grid grid-cols-2 gap-5">
            {/* Today's schedule */}
            <div className="card overflow-hidden">
              <div className="px-5 py-4 border-b border-border flex items-center justify-between">
                <h2 className="font-bold text-gray-800">Today's Schedule</h2>
                <button className="text-xs text-primary-600 font-semibold hover:underline"
                  onClick={() => navigate('/appointments')}>View all</button>
              </div>
              <div className="divide-y divide-border">
                {data?.todaySchedule.length === 0 && (
                  <p className="text-gray-400 text-sm text-center py-8">No appointments today</p>
                )}
                {data?.todaySchedule.map(a => (
                  <div key={a.id} className="flex items-center gap-3 px-5 py-3 hover:bg-gray-50">
                    <Avatar name={a.patientName} size="sm" />
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-sm text-gray-800 truncate">{a.patientName}</p>
                      <p className="text-xs text-gray-400">{a.type} · {a.durationMinutes}min</p>
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
                <h2 className="font-bold text-gray-800">Recent Patients</h2>
                <button className="text-xs text-primary-600 font-semibold hover:underline"
                  onClick={() => navigate('/patients')}>View all</button>
              </div>
              <div className="divide-y divide-border">
                {data?.recentPatients.length === 0 && (
                  <p className="text-gray-400 text-sm text-center py-8">No patients yet</p>
                )}
                {data?.recentPatients.map(p => (
                  <div key={p.id} className="flex items-center gap-3 px-5 py-3 hover:bg-gray-50 cursor-pointer"
                    onClick={() => navigate(`/patients/${p.id}`)}>
                    <Avatar name={p.fullName} size="sm" />
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-sm text-gray-800 truncate">{p.fullName}</p>
                      <p className="text-xs text-gray-400">{p.primaryCondition ?? 'No condition listed'} · Age {p.age}</p>
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
