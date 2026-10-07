import { useTranslation } from 'react-i18next'
import type { ParseKeys } from 'i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Trash2 } from 'lucide-react'
import {
  useClinic, useRenameClinic, useStaff, useStaffInvitations, useInviteStaff, useRevokeStaffInvitation,
  useChangeStaffRole, useSetStaffActive,
} from '@/hooks/queries'
import { useAuthStore } from '@/store/authStore'
import { PageHeader } from '@/components/layout/AppLayout'
import { EmptyState, PageSpinner, Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import { PERMISSIONS, STAFF_ROLES, type Permission } from '@/utils/permissions'
import type { ClinicRole, InvitableRole } from '@/types'

const INVITABLE: InvitableRole[] = ['Doctor', 'Nurse', 'Receptionist']

const inviteSchema = z.object({
  email: z.string().email('validation.invalidEmail'),
  role: z.enum(['Doctor', 'Nurse', 'Receptionist']),
})
type InviteForm = z.infer<typeof inviteSchema>

const clinicSchema = z.object({ name: z.string().trim().min(1, 'validation.required').max(200, 'validation.required') })
type ClinicForm = z.infer<typeof clinicSchema>

/** Staff management: Owners only (the route is guarded and the API enforces it again). */
export function StaffPage() {
  const { t } = useTranslation()
  const me = useAuthStore(s => s.user)
  const { data: clinic } = useClinic()
  const { data: staff, isLoading } = useStaff()
  const { data: invitations } = useStaffInvitations()
  const invite = useInviteStaff()
  const revoke = useRevokeStaffInvitation()
  const changeRole = useChangeStaffRole()
  const setActive = useSetStaffActive()
  const rename = useRenameClinic()

  const inviteForm = useForm<InviteForm>({ resolver: zodResolver(inviteSchema), defaultValues: { role: 'Nurse' } })
  const clinicForm = useForm<ClinicForm>({ resolver: zodResolver(clinicSchema), values: { name: clinic?.name ?? '' } })

  if (isLoading) return <PageSpinner />

  return (
    <>
      <PageHeader title={t('staff.title')} subtitle={t('staff.subtitle', { clinic: clinic?.name ?? '' })} />
      <div className="flex-1 overflow-y-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6 space-y-6">
        <section className="card p-5">
          <form onSubmit={clinicForm.handleSubmit(v => rename.mutate(v.name))} className="flex flex-wrap items-end gap-3">
            <div className="min-w-[220px] flex-1">
              <label className="label" htmlFor="clinic-name">{t('clinic.nameLabel')}</label>
              <input id="clinic-name" className="input" maxLength={200} {...clinicForm.register('name')} />
              {clinicForm.formState.errors.name && <p className="text-red-500 text-xs mt-1">{t('validation.required')}</p>}
            </div>
            <button type="submit" className="btn-secondary" disabled={rename.isPending}>{t('clinic.save')}</button>
          </form>
        </section>

        <section className="card p-5">
          <h2 className="text-base font-semibold text-gray-900 mb-1">{t('staff.invite.title')}</h2>
          <p className="text-xs text-gray-500 mb-4">{t('staff.invite.hint')}</p>
          <form onSubmit={inviteForm.handleSubmit(v => invite.mutate(v, { onSuccess: () => inviteForm.reset({ email: '', role: v.role }) }))}
            className="flex flex-wrap items-start gap-3">
            <div className="min-w-[220px] flex-1">
              <label className="label" htmlFor="invite-email">{t('staff.invite.email')}</label>
              <input id="invite-email" type="email" className="input" autoComplete="off" {...inviteForm.register('email')} />
              {inviteForm.formState.errors.email && (
                <p className="text-red-500 text-xs mt-1">{t(inviteForm.formState.errors.email.message as ParseKeys)}</p>
              )}
            </div>
            <div className="min-w-[180px]">
              <label className="label" htmlFor="invite-role">{t('staff.invite.role')}</label>
              <select id="invite-role" className="input" {...inviteForm.register('role')}>
                {INVITABLE.map(r => <option key={r} value={r}>{t(`roles.${r}`)}</option>)}
              </select>
            </div>
            <button type="submit" className="btn-primary mt-6" disabled={invite.isPending}>
              {invite.isPending ? <Spinner className="w-4 h-4" /> : t('staff.invite.send')}
            </button>
          </form>
        </section>

        <section className="card overflow-x-auto">
          <h2 className="px-5 pt-5 text-base font-semibold text-gray-900">{t('staff.members.title')}</h2>
          <table className="w-full min-w-[720px] mt-3">
            <thead>
              <tr className="bg-gray-50 border-y border-border">
                {(['name', 'email', 'role', 'status', 'joined'] as const).map(h => (
                  <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">{t(`staff.members.${h}`)}</th>
                ))}
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {staff?.items.map(m => {
                const isMe = m.userId === me?.id
                const name = `${m.firstName} ${m.lastName}`.trim()
                return (
                  <tr key={m.id}>
                    <td className="px-4 py-3 text-sm font-medium text-gray-900">
                      {name} {isMe && <span className="text-xs text-gray-400">({t('staff.members.you')})</span>}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">{m.email}</td>
                    <td className="px-4 py-3">
                      <select className="input py-1.5" value={m.role} disabled={changeRole.isPending}
                        aria-label={t('staff.roleFor', { name })}
                        onChange={e => changeRole.mutate({ id: m.id, role: e.target.value as ClinicRole })}>
                        {STAFF_ROLES.map(r => <option key={r} value={r}>{t(`roles.${r}`)}</option>)}
                      </select>
                    </td>
                    <td className="px-4 py-3 text-sm">
                      <span className={`badge ${m.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-gray-100 text-gray-500'}`}>
                        {m.isActive ? t('staff.active') : t('staff.inactive')}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">{fmt.date(m.joinedAt)}</td>
                    <td className="px-4 py-3 text-right">
                      {m.isActive ? (
                        <button className="btn-secondary text-xs" disabled={setActive.isPending}
                          onClick={() => { if (confirm(t('staff.confirmDeactivate'))) setActive.mutate({ id: m.id, active: false }) }}>
                          {t('staff.deactivate')}
                        </button>
                      ) : (
                        <button className="btn-secondary text-xs" disabled={setActive.isPending}
                          onClick={() => setActive.mutate({ id: m.id, active: true })}>
                          {t('staff.reactivate')}
                        </button>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </section>

        <section className="card p-5">
          <h2 className="text-base font-semibold text-gray-900 mb-3">{t('staff.pending.title')}</h2>
          {!invitations || invitations.items.length === 0 ? (
            <EmptyState title={t('staff.pending.empty')} />
          ) : (
            <ul className="divide-y divide-gray-100">
              {invitations.items.map(i => (
                <li key={i.id} className="py-3 flex flex-wrap items-center justify-between gap-2">
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-gray-900 break-all">{i.email}</p>
                    <p className="text-xs text-gray-500">{t(`roles.${i.role}`)} · {t('staff.pending.expires')} {fmt.date(i.expiresAt)}</p>
                  </div>
                  <button className="btn-secondary" disabled={revoke.isPending}
                    aria-label={`${t('staff.pending.revoke')} ${i.email}`} onClick={() => revoke.mutate(i.id)}>
                    <Trash2 size={14} /> {t('staff.pending.revoke')}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="card p-5 overflow-x-auto">
          <h2 className="text-base font-semibold text-gray-900 mb-3">{t('staff.permissions')}</h2>
          <ul className="space-y-1.5 mb-4">
            {STAFF_ROLES.map(r => (
              <li key={r} className="text-sm text-gray-700"><strong>{t(`roles.${r}`)}:</strong> {t(`roleHelp.${r}`)}</li>
            ))}
          </ul>
          <details>
            <summary className="text-xs text-primary-600 cursor-pointer">{t('staff.permissions')}</summary>
            <table className="mt-3 text-xs min-w-[480px]">
              <thead>
                <tr><th className="text-left pr-4 py-1">&nbsp;</th>{STAFF_ROLES.map(r => <th key={r} className="px-2 py-1">{t(`roles.${r}`)}</th>)}</tr>
              </thead>
              <tbody>
                {(Object.keys(PERMISSIONS) as Permission[]).map(p => (
                  <tr key={p} className="border-t border-border">
                    <td className="pr-4 py-1 font-mono text-gray-600">{p}</td>
                    {STAFF_ROLES.map(r => (
                      <td key={r} className="px-2 py-1 text-center">{PERMISSIONS[p].includes(r) ? '✓' : '—'}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </details>
        </section>
      </div>
    </>
  )
}
