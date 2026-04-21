import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import {
  LayoutDashboard, Users, CalendarDays, Pill, CreditCard, LogOut, Plus
} from 'lucide-react'

const NAV = [
  { to: '/',              icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/patients',      icon: Users,           label: 'Patients' },
  { to: '/appointments',  icon: CalendarDays,    label: 'Appointments' },
  { to: '/prescriptions', icon: Pill,            label: 'Prescriptions' },
  { to: '/billing',       icon: CreditCard,      label: 'Billing' },
]

export function AppLayout() {
  const { user, logout } = useAuthStore()
  const navigate = useNavigate()

  const handleLogout = () => { logout(); navigate('/login') }

  return (
    <div className="flex h-screen overflow-hidden">
      {/* Sidebar */}
      <aside className="w-[220px] bg-navy-800 flex flex-col py-7 flex-shrink-0">
        {/* Logo */}
        <div className="px-6 pb-7 border-b border-navy-600">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center text-white font-bold text-sm">✚</div>
            <span className="text-white font-bold text-lg tracking-tight">MedFlow</span>
          </div>
          <p className="text-navy-500 text-xs mt-1 pl-[42px]">Patient Management</p>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-3 pt-4 space-y-0.5">
          {NAV.map(({ to, icon: Icon, label }) => (
            <NavLink key={to} to={to} end={to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all ${
                  isActive
                    ? 'bg-navy-600 text-primary-400'
                    : 'text-[#7A9BB5] hover:text-white hover:bg-navy-700'
                }`
              }>
              <Icon size={16} />
              {label}
            </NavLink>
          ))}
        </nav>

        {/* Doctor profile */}
        <div className="px-4 pt-4 border-t border-navy-600">
          <div className="flex items-center gap-2.5 mb-3">
            <div className="w-8 h-8 rounded-full bg-gradient-to-br from-primary-400 to-primary-700 flex items-center justify-center text-white font-bold text-xs flex-shrink-0">
              {user?.firstName?.[0]}{user?.lastName?.[0]}
            </div>
            <div className="min-w-0">
              <p className="text-[#C8D8E8] text-xs font-semibold truncate">Dr. {user?.firstName} {user?.lastName}</p>
              <p className="text-[#4A6280] text-xs truncate">{user?.specialty}</p>
            </div>
          </div>
          <button onClick={handleLogout} className="flex items-center gap-2 text-[#4A6280] hover:text-red-400 text-xs transition-colors w-full px-1 py-1">
            <LogOut size={13} /> Sign out
          </button>
        </div>
      </aside>

      {/* Main */}
      <div className="flex-1 flex flex-col overflow-hidden">
        <Outlet />
      </div>
    </div>
  )
}

// ── Page header reusable component ────────────────────────────────────────────
interface PageHeaderProps {
  title: string; subtitle?: string
  action?: { label: string; onClick: () => void }
  children?: React.ReactNode
}
export function PageHeader({ title, subtitle, action, children }: PageHeaderProps) {
  return (
    <div className="bg-white border-b border-border px-8 py-4 flex items-center justify-between flex-shrink-0">
      <div>
        <h1 className="text-xl font-bold text-gray-900">{title}</h1>
        {subtitle && <p className="text-sm text-gray-400 mt-0.5">{subtitle}</p>}
      </div>
      <div className="flex items-center gap-3">
        {children}
        {action && (
          <button className="btn-primary" onClick={action.onClick}>
            <Plus size={15} /> {action.label}
          </button>
        )}
      </div>
    </div>
  )
}
