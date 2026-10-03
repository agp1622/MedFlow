import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuditLog } from '@/hooks/queries'
import { PageSpinner, EmptyState, Pagination } from '@/components/ui'
import { fmt, displayEnum } from '@/utils/format'
import type { AuditAction, AuditLogQuery } from '@/types'

const PAGE_SIZE = 20

export function AuditLogTab({ patientId }: { patientId: number }) {
  const { t } = useTranslation()
  const [action, setAction] = useState<AuditAction | ''>('')
  const [actor, setActor] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)

  const invalidRange = !!from && !!to && from > to
  const query: AuditLogQuery = {
    page, pageSize: PAGE_SIZE,
    action: action || undefined,
    actor: actor.trim() || undefined,
    from: from || undefined,
    to: to || undefined,
  }
  const { data, isLoading, isError } = useAuditLog(patientId, invalidRange ? { page: 1, pageSize: 1 } : query)

  const update = (fn: () => void) => { fn(); setPage(1) }
  const filtered = !!(action || actor.trim() || from || to)

  return (
    <div className="card p-4 space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <label className="text-sm">
          <span className="block text-gray-500 mb-1">{t('audit.action')}</span>
          <select className="input" value={action} onChange={e => update(() => setAction(e.target.value as AuditAction | ''))}>
            <option value="">{t('common.all')}</option>
            <option value="View">{displayEnum('View')}</option>
            <option value="Change">{displayEnum('Change')}</option>
          </select>
        </label>
        <label className="text-sm">
          <span className="block text-gray-500 mb-1">{t('audit.user')}</span>
          <input className="input" value={actor} maxLength={200} placeholder={t('audit.userPlaceholder')}
            onChange={e => update(() => setActor(e.target.value))} />
        </label>
        <label className="text-sm">
          <span className="block text-gray-500 mb-1">{t('audit.from')}</span>
          <input type="date" className="input" value={from} onChange={e => update(() => setFrom(e.target.value))} />
        </label>
        <label className="text-sm">
          <span className="block text-gray-500 mb-1">{t('audit.to')}</span>
          <input type="date" className="input" value={to} onChange={e => update(() => setTo(e.target.value))} />
        </label>
      </div>

      {invalidRange && <p className="text-sm text-red-600">{t('audit.invalidRange')}</p>}

      {!invalidRange && isLoading && <PageSpinner />}
      {!invalidRange && isError && <p className="text-sm text-red-600">{t('audit.loadError')}</p>}
      {!invalidRange && data && data.items.length === 0 && (
        <EmptyState title={filtered ? t('audit.noMatch') : t('audit.none')}
          description={filtered ? t('audit.widen') : t('audit.noneHint')} />
      )}
      {!invalidRange && data && data.items.length > 0 && (
        <>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="bg-gray-50 border-b border-border">
                  {(['when', 'user', 'action', 'item', 'fields'] as const).map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">{t(`audit.cols.${h}`)}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data.items.map(e => (
                  <tr key={e.id}>
                    <td className="px-4 py-3 text-sm text-gray-700 whitespace-nowrap">{fmt.timestamp(e.occurredAt)}</td>
                    <td className="px-4 py-3 text-sm text-gray-700">{e.actorName} <span className="text-gray-400">({displayEnum(e.actorRole)})</span></td>
                    <td className="px-4 py-3 text-sm text-gray-700">{displayEnum(e.action)}</td>
                    <td className="px-4 py-3 text-sm text-gray-700">{displayEnum(e.itemKind)}{e.itemId != null ? ` #${e.itemId}` : ''}</td>
                    <td className="px-4 py-3 text-sm text-gray-700">{e.changedFields.length ? e.changedFields.join(', ') : '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={data.page} totalPages={data.totalPages} onPage={setPage} />
        </>
      )}
    </div>
  )
}
