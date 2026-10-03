import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { ThemeToggle } from '@/components/ui/ThemeToggle'
import {
  LayoutDashboard, Users, CalendarDays, Pill, CreditCard, LogOut, Plus, Menu, X, ClipboardList, Clock, FileText
} from 'lucide-react'

const NAV = [
  { to: '/',              icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/patients',      icon: Users,           label: 'Patients' },
  { to: '/intake',        icon: ClipboardList,   label: 'Intake forms' },
  { to: '/appointments',  icon: CalendarDays,    label: 'Appointments' },
  { to: '/availability',  icon: Clock,           label: 'Availability' },
  { to: '/prescriptions', icon: Pill,            label: 'Prescriptions' },
  { to: '/billing',       icon: CreditCard,      label: 'Billing' },
  { to: '/templates',     icon: FileText,        label: 'Note Templates' },
]

export function AppLayout() {
  const { user, logout } = useAuthStore()
  const navigate = useNavigate()

  const location = useLocation()
  const [navOpen, setNavOpen] = useState(false)

  const handleLogout = () => { logout(); navigate('/login') }

  // Close the mobile drawer on navigation and on Escape.
  useEffect(() => { setNavOpen(false) }, [location.pathname])
  useEffect(() => {
    if (!navOpen) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setNavOpen(false) }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [navOpen])

  return (
    <div className="flex h-screen h-dvh overflow-hidden">
      {/* Mobile backdrop */}
      {navOpen && (
        <div className="fixed inset-0 z-30 bg-black/50 lg:hidden" aria-hidden="true" onClick={() => setNavOpen(false)} />
      )}

      {/* Sidebar: off-canvas drawer below lg, fixed column at lg and up */}
      <aside id="app-navigation"
        className={`fixed inset-y-0 left-0 z-40 w-[260px] max-w-[85vw] overflow-y-auto transform transition-transform duration-200 lg:static lg:z-auto lg:w-[220px] lg:max-w-none lg:translate-x-0 lg:overflow-visible bg-navy-800 flex flex-col py-7 flex-shrink-0 ${
          navOpen ? 'translate-x-0' : '-translate-x-full'
        }`}>
        {/* Logo */}
        <div className="relative px-6 pb-7 border-b border-navy-600">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center text-white font-bold text-sm">✚</div>
            <span className="text-white font-bold text-lg tracking-tight">MedFlow</span>
          </div>
          <p className="text-navy-500 text-xs mt-1 pl-[42px]">Patient Management</p>
          <button type="button" className="lg:hidden absolute top-4 right-3 p-2 text-white/70 hover:text-white"
            aria-label="Close navigation" onClick={() => setNavOpen(false)}>
            <X size={18} />
          </button>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-3 pt-4 space-y-0.5">
          {NAV.map(({ to, icon: Icon, label }) => (
            <NavLink key={to} to={to} end={to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-3 lg:py-2.5 rounded-lg text-sm font-medium transition-all ${
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
          <div className="flex items-center justify-between">
            <button onClick={handleLogout} className="flex items-center gap-2 text-[#4A6280] hover:text-red-400 text-xs transition-colors px-1 py-1">
              <LogOut size={13} /> Sign out
            </button>
            <ThemeToggle />
          </div>
        </div>
      </aside>

      {/* Main */}
      <div className="flex-1 min-w-0 flex flex-col overflow-hidden">
        {/* Mobile top bar */}
        <div className="lg:hidden flex items-center gap-3 bg-navy-800 px-3 h-14 flex-shrink-0">
          <button type="button" className="p-2.5 -ml-1 text-white/90 hover:text-white"
            aria-label="Open navigation" aria-expanded={navOpen} aria-controls="app-navigation"
            onClick={() => setNavOpen(true)}>
            <Menu size={20} />
          </button>
          <span className="text-white font-bold text-lg tracking-tight">MedFlow</span>
        </div>
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
    <div className="bg-white border-b border-border px-4 sm:px-6 lg:px-8 py-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between flex-shrink-0">
      <div className="min-w-0">
        <h1 className="text-xl font-bold text-gray-900 break-words">{title}</h1>
        {subtitle && <p className="text-sm text-gray-400 mt-0.5">{subtitle}</p>}
      </div>
      <div className="flex flex-wrap items-center gap-3">
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
