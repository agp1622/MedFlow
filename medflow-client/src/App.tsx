import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import { AppLayout } from '@/components/layout/AppLayout'
import { LoginPage, RegisterPage, ForgotPasswordPage, ResetPasswordPage, AcceptInvitePage, AcceptStaffInvitePage, AuthShell } from '@/pages/AuthPages'
import { StaffPage } from '@/pages/StaffPage'
import { SecurityPage } from '@/pages/SecurityPage'
import { can, isStaffRole, type Permission } from '@/utils/permissions'
import { useTranslation } from 'react-i18next'
import { AppointmentResponsePage } from '@/pages/AppointmentResponsePage'
import { PortalLayout } from '@/components/layout/PortalLayout'
import { PortalPage } from '@/pages/PortalPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { PatientsPage, PatientDetailPage } from '@/pages/PatientsPage'
import { NoteTemplatesPage } from '@/pages/NoteTemplatesPage'
import { ReportsPage } from '@/pages/ReportsPage'
import { AppointmentsPage } from '@/pages/AppointmentsPage'
import { IntakeFormPage, IntakeListPage, IntakeReviewPage } from '@/pages/IntakePages'
import { AvailabilityPage } from '@/pages/AvailabilityPage'
import { WaitlistPage } from '@/pages/WaitlistPage'
import { WaitlistOfferPage } from '@/pages/WaitlistOfferPage'
import { PrescriptionsPage, BillingPage } from '@/pages/BillingPrescriptionsPages'

const homeFor = (role?: string) => (role === 'Patient' ? '/portal' : '/')

// Staff area (Owner, Doctor, Nurse, Receptionist): patients are sent to their portal
function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuthStore()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (user?.role === 'Patient') return <Navigate to="/portal" replace />
  return isStaffRole(user?.role) ? <>{children}</> : <NoClinicAccess />
}

// An account that belongs to no active clinic has nothing to see (the API answers 403 for all of it)
function NoClinicAccess() {
  const { t } = useTranslation()
  const logout = useAuthStore(s => s.logout)
  return (
    <AuthShell title={t('noAccess.title')} subtitle="">
      <div className="space-y-4 text-center">
        <p className="text-sm text-gray-700">{t('noAccess.body')}</p>
        <button className="btn-primary w-full h-10" onClick={logout}>{t('layout.signOut')}</button>
      </div>
    </AuthShell>
  )
}

// Hides a page from roles without the permission (the API enforces the same matrix)
function Guard({ permission, children }: { permission: Permission; children: React.ReactNode }) {
  const role = useAuthStore(s => s.user?.role)
  return can(role, permission) ? <>{children}</> : <Navigate to="/" replace />
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

      {/* Same for staff invitation links */}
      <Route path="/accept-staff-invite" element={<AcceptStaffInvitePage />} />

      {/* Public, token-gated intake form: no account needed, works whether or not someone is signed in */}
      <Route path="/intake/:token" element={<IntakeFormPage />} />

      {/* Public: opened from the reminder email, authorised by its token only */}
      <Route path="/appointment-response" element={<AppointmentResponsePage />} />
      <Route path="/waitlist-offer" element={<WaitlistOfferPage />} />

      <Route path="portal" element={<PatientRoute><PortalLayout /></PatientRoute>}>
        <Route index element={<PortalPage />} />
      </Route>

      <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
        <Route index element={<DashboardPage />} />
        <Route path="patients" element={<Guard permission="PatientsRead"><PatientsPage /></Guard>} />
        <Route path="patients/:id" element={<Guard permission="PatientsRead"><PatientDetailPage /></Guard>} />
        <Route path="intake" element={<Guard permission="IntakeReview"><IntakeListPage /></Guard>} />
        <Route path="intake/review/:id" element={<Guard permission="IntakeReview"><IntakeReviewPage /></Guard>} />
        <Route path="appointments" element={<Guard permission="AppointmentsRead"><AppointmentsPage /></Guard>} />
        <Route path="availability" element={<Guard permission="AvailabilityManage"><AvailabilityPage /></Guard>} />
        <Route path="waitlist" element={<Guard permission="WaitlistManage"><WaitlistPage /></Guard>} />
        <Route path="prescriptions" element={<Guard permission="PrescriptionsRead"><PrescriptionsPage /></Guard>} />
        <Route path="templates" element={<Guard permission="NoteTemplates"><NoteTemplatesPage /></Guard>} />
        <Route path="billing" element={<Guard permission="InvoicesRead"><BillingPage /></Guard>} />
        <Route path="reports" element={<Guard permission="ReportsRead"><ReportsPage /></Guard>} />
        <Route path="security" element={<SecurityPage />} />
        <Route path="staff" element={<Guard permission="StaffManage"><StaffPage /></Guard>} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
