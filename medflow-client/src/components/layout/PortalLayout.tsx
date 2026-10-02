import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useUnreadMessages } from '@/hooks/queries'
import { useAuthStore } from '@/store/authStore'
import { ThemeToggle } from '@/components/ui/ThemeToggle'
import { LogOut } from 'lucide-react'

export function PortalLayout() {
  const { user, logout } = useAuthStore()
  const navigate = useNavigate()
  const unread = useUnreadMessages('Patient').data ?? 0
  const handleLogout = () => { logout(); navigate('/login') }

  return (
    <div className="min-h-screen bg-surface">
      <header className="bg-navy-800">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 h-14 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center text-white font-bold text-sm">✚</div>
            <span className="text-white font-bold text-lg tracking-tight">MedFlow</span>
            <span className="hidden sm:inline text-navy-300 text-sm ml-2">Patient portal</span>
          </div>
          <div className="flex items-center gap-3">
            <span className="hidden sm:inline text-sm text-white/80">{user?.firstName} {user?.lastName}</span>
            <ThemeToggle />
            <button onClick={handleLogout} className="text-white/80 hover:text-white p-1.5" title="Sign out" aria-label="Sign out">
              <LogOut size={18} />
            </button>
          </div>
        </div>
      </header>
      <nav className="border-b border-border" aria-label="Portal">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 flex gap-5 text-sm font-medium">
          {[{ to: '/portal', label: 'My records', end: true }, { to: '/portal/messages', label: 'Messages', end: false }].map(l => (
            <NavLink key={l.to} to={l.to} end={l.end}
              className={({ isActive }) =>
                `py-3 border-b-2 flex items-center gap-2 ${isActive ? 'border-primary-600 text-primary-600' : 'border-transparent text-gray-500 hover:text-gray-800'}`}>
              {l.label}
              {l.to.endsWith('messages') && unread > 0 && (
                <span className="bg-primary-600 text-white text-[11px] font-bold rounded-full min-w-[20px] h-5 px-1.5 flex items-center justify-center"
                  aria-label={`${unread} unread messages`}>{unread}</span>
              )}
            </NavLink>
          ))}
        </div>
      </nav>
      <main className="max-w-5xl mx-auto px-4 sm:px-6 py-6">
        <Outlet />
      </main>
    </div>
  )
}
