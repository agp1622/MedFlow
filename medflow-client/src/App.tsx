import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { AppLayout } from '@/components/layout/AppLayout'
import { LoginPage, RegisterPage, ForgotPasswordPage, ResetPasswordPage, AcceptInvitePage } from '@/pages/AuthPages'
import { PortalLayout } from '@/components/layout/PortalLayout'
import { PortalPage } from '@/pages/PortalPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { PatientsPage, PatientDetailPage } from '@/pages/PatientsPage'
import { AppointmentsPage } from '@/pages/AppointmentsPage'
import { PrescriptionsPage, BillingPage } from '@/pages/BillingPrescriptionsPages'

const homeFor = (role?: string) => (role === 'Patient' ? '/portal' : '/')

// Doctor-only area: patients are sent to their portal
function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuthStore()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return user?.role === 'Doctor' ? <>{children}</> : <Navigate to={homeFor(user?.role)} replace />
}

// Patient-only area: doctors are sent to the dashboard
function PatientRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuthStore()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return user?.role === 'Patient' ? <>{children}</> : <Navigate to="/" replace />
}

function PublicRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuthStore()
  return !isAuthenticated ? <>{children}</> : <Navigate to={homeFor(user?.role)} replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<PublicRoute><LoginPage /></PublicRoute>} />
      <Route path="/register" element={<PublicRoute><RegisterPage /></PublicRoute>} />
      <Route path="/forgot-password" element={<PublicRoute><ForgotPasswordPage /></PublicRoute>} />
      <Route path="/reset-password" element={<PublicRoute><ResetPasswordPage /></PublicRoute>} />

      {/* Not wrapped in PublicRoute: opening an invitation link must work even if someone is signed in */}
      <Route path="/accept-invite" element={<AcceptInvitePage />} />

      <Route path="portal" element={<PatientRoute><PortalLayout /></PatientRoute>}>
        <Route index element={<PortalPage />} />
      </Route>

      <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
        <Route index element={<DashboardPage />} />
        <Route path="patients" element={<PatientsPage />} />
        <Route path="patients/:id" element={<PatientDetailPage />} />
        <Route path="appointments" element={<AppointmentsPage />} />
        <Route path="prescriptions" element={<PrescriptionsPage />} />
        <Route path="billing" element={<BillingPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
