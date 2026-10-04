import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ParseKeys } from 'i18next'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { useClinic } from '@/hooks/queries'
import { can, type Permission } from '@/utils/permissions'
import { ThemeToggle } from '@/components/ui/ThemeToggle'
import { LanguageSwitcher } from '@/components/ui/LanguageSwitcher'
import {
  LayoutDashboard, Users, CalendarDays, Pill, CreditCard, LogOut, Plus, Menu, X, ClipboardList, Clock, FileText, BarChart3, ListOrdered, UserCog
} from 'lucide-react'

// Each entry is shown only to roles holding its permission (same matrix the API enforces)
const NAV: { to: string; icon: typeof Users; label: ParseKeys; permission: Permission }[] = [
  { to: '/',              icon: LayoutDashboard, label: 'nav.dashboard',     permission: 'DashboardRead' },
  { to: '/patients',      icon: Users,           label: 'nav.patients',      permission: 'PatientsRead' },
  { to: '/intake',        icon: ClipboardList,   label: 'nav.intake',        permission: 'IntakeReview' },
  { to: '/appointments',  icon: CalendarDays,    label: 'nav.appointments',  permission: 'AppointmentsRead' },
  { to: '/availability',  icon: Clock,           label: 'nav.availability',  permission: 'AvailabilityManage' },
  { to: '/waitlist',      icon: ListOrdered,     label: 'nav.waitlist',      permission: 'WaitlistManage' },
  { to: '/prescriptions', icon: Pill,            label: 'nav.prescriptions', permission: 'PrescriptionsRead' },
  { to: '/billing',       icon: CreditCard,      label: 'nav.billing',       permission: 'InvoicesRead' },
  { to: '/templates',     icon: FileText,        label: 'nav.templates',     permission: 'NoteTemplates' },
  { to: '/reports',       icon: BarChart3,       label: 'nav.reports',       permission: 'ReportsRead' },
  { to: '/staff',         icon: UserCog,         label: 'nav.staff',         permission: 'StaffManage' },
]

export function AppLayout() {
  const { t } = useTranslation()
  const { user, logout, updateUser } = useAuthStore()
  const navigate = useNavigate()
  // The role is re-read from the API: it can change (or be withdrawn) while a token is still valid
  const { data: clinic, error: clinicError } = useClinic()
  useEffect(() => {
    if (clinic && (clinic.role !== user?.role || clinic.name !== user?.clinicName))
      updateUser({ role: clinic.role, clinicName: clinic.name, clinicId: clinic.id })
  }, [clinic, user?.role, user?.clinicName, updateUser])
  useEffect(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any -- axios error shape is untyped here
    if ((clinicError as any)?.response?.status === 403) { logout(); navigate('/login') }
  }, [clinicError, logout, navigate])
  const role = user?.role

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
          <p className="text-navy-500 text-xs mt-1 pl-[42px]">{t('app.tagline')}</p>
          <button type="button" className="lg:hidden absolute top-4 right-3 p-2 text-white/70 hover:text-white"
            aria-label={t('nav.close')} onClick={() => setNavOpen(false)}>
            <X size={18} />
          </button>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-3 pt-4 space-y-0.5">
          {NAV.filter(n => can(role, n.permission)).map(({ to, icon: Icon, label }) => (
            <NavLink key={to} to={to} end={to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-3 lg:py-2.5 rounded-lg text-sm font-medium transition-all ${
                  isActive
                    ? 'bg-navy-600 text-primary-400'
                    : 'text-[#7A9BB5] hover:text-white hover:bg-navy-700'
                }`
              }>
              <Icon size={16} />
              {t(label)}
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
              <p className="text-[#C8D8E8] text-xs font-semibold truncate">
                {(role === 'Owner' || role === 'Doctor') && `${t('layout.doctorPrefix')} `}{user?.firstName} {user?.lastName}
              </p>
              <p className="text-[#4A6280] text-xs truncate">{role ? t(`roles.${role}`) : ''}{user?.clinicName ? ` · ${user.clinicName}` : ''}</p>
            </div>
          </div>
          <div className="flex items-center justify-between">
            <button onClick={handleLogout} className="flex items-center gap-2 text-[#4A6280] hover:text-red-400 text-xs transition-colors px-1 py-1">
              <LogOut size={13} /> {t('layout.signOut')}
            </button>
            <ThemeToggle />
          </div>
          <LanguageSwitcher className="mt-3" />
        </div>
      </aside>

      {/* Main */}
      <div className="flex-1 min-w-0 flex flex-col overflow-hidden">
        {/* Mobile top bar */}
        <div className="lg:hidden flex items-center gap-3 bg-navy-800 px-3 h-14 flex-shrink-0">
          <button type="button" className="p-2.5 -ml-1 text-white/90 hover:text-white"
            aria-label={t('nav.open')} aria-expanded={navOpen} aria-controls="app-navigation"
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
