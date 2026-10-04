import type { ClinicRole, UserRole } from '@/types'

/**
 * Mirror of MedFlow.Core.PermissionMatrix (specs/045-clinic-roles/contracts/permission-matrix.md).
 * It only decides what to show: the API enforces the same matrix on every request, so a stale or edited
 * client can never gain access.
 */
export type Permission =
  | 'ClinicRead' | 'DashboardRead'
  | 'PatientsRead' | 'PatientsWrite' | 'PatientsDelete' | 'PatientClinicalFields'
  | 'AppointmentsRead' | 'AppointmentsWrite' | 'WaitlistManage' | 'AvailabilityManage'
  | 'IntakeLinkSend' | 'IntakeReview' | 'PortalInvite'
  | 'PrescriptionsRead' | 'PrescriptionsWrite' | 'InvoicesRead' | 'InvoicesWrite'
  | 'VitalsRead' | 'VitalsWrite' | 'NotesRead' | 'NotesWrite' | 'NotesManage' | 'NoteTemplates'
  | 'AttachmentsRead' | 'AttachmentsWrite' | 'ClinicalListsRead' | 'ClinicalListsWrite'
  | 'LabsRead' | 'LabsWrite' | 'AuditLogRead' | 'ReportsRead' | 'StaffManage'

const O: ClinicRole = 'Owner', D: ClinicRole = 'Doctor', N: ClinicRole = 'Nurse', R: ClinicRole = 'Receptionist'

export const PERMISSIONS: Record<Permission, ClinicRole[]> = {
  ClinicRead: [O, D, N, R],
  DashboardRead: [O, D, N, R],
  PatientsRead: [O, D, N, R],
  PatientsWrite: [O, D, R],
  PatientsDelete: [O, D],
  PatientClinicalFields: [O, D, N],
  AppointmentsRead: [O, D, N, R],
  AppointmentsWrite: [O, D, R],
  WaitlistManage: [O, D, R],
  AvailabilityManage: [O, D],
  IntakeLinkSend: [O, D, R],
  IntakeReview: [O, D],
  PortalInvite: [O, D, R],
  PrescriptionsRead: [O, D, N],
  PrescriptionsWrite: [O, D],
  InvoicesRead: [O, D, R],
  InvoicesWrite: [O, D, R],
  VitalsRead: [O, D, N],
  VitalsWrite: [O, D, N],
  NotesRead: [O, D, N],
  NotesWrite: [O, D, N],
  NotesManage: [O, D],
  NoteTemplates: [O, D, N],
  AttachmentsRead: [O, D, N],
  AttachmentsWrite: [O, D],
  ClinicalListsRead: [O, D, N],
  ClinicalListsWrite: [O, D],
  LabsRead: [O, D, N],
  LabsWrite: [O, D],
  AuditLogRead: [O, D],
  ReportsRead: [O, D],
  StaffManage: [O],
}

export const STAFF_ROLES: ClinicRole[] = ['Owner', 'Doctor', 'Nurse', 'Receptionist']

export const isStaffRole = (role?: UserRole | string | null): role is ClinicRole =>
  !!role && (STAFF_ROLES as string[]).includes(role)

/** False for patients, for no role and for roles without the permission. */
export const can = (role: UserRole | string | null | undefined, permission: Permission): boolean =>
  isStaffRole(role) && PERMISSIONS[permission].includes(role)
