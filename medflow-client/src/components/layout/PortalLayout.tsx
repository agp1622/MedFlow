import { useTranslation } from 'react-i18next'
import { Outlet, useNavigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { ThemeToggle } from '@/components/ui/ThemeToggle'
import { LanguageSwitcher } from '@/components/ui/LanguageSwitcher'
import { LogOut } from 'lucide-react'

export function PortalLayout() {
  const { t } = useTranslation()
  const { user, logout } = useAuthStore()
  const navigate = useNavigate()
  const handleLogout = () => { logout(); navigate('/login') }

  return (
    <div className="min-h-screen bg-surface">
      <header className="bg-navy-800">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 h-14 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary-400 to-primary-600 flex items-center justify-center text-white font-bold text-sm">✚</div>
            <span className="text-white font-bold text-lg tracking-tight">MedFlow</span>
            <span className="hidden sm:inline text-navy-300 text-sm ml-2">{t('portal.title')}</span>
          </div>
          <div className="flex items-center gap-3">
            <span className="hidden sm:inline text-sm text-white/80">{user?.firstName} {user?.lastName}</span>
            <LanguageSwitcher />
            <ThemeToggle />
            <button onClick={handleLogout} className="text-white/80 hover:text-white p-1.5" title={t('layout.signOut')} aria-label={t('layout.signOut')}>
              <LogOut size={18} />
            </button>
          </div>
        </div>
      </header>
      <main className="max-w-5xl mx-auto px-4 sm:px-6 py-6">
        <Outlet />
      </main>
    </div>
  )
}
