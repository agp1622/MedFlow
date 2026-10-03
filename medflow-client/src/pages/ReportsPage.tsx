import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import toast from 'react-hot-toast'
import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Download } from 'lucide-react'
import { reportsApi, type ReportKind } from '@/api/services'
import { useArAgingReport, useNoShowReport, useRevenueReport, useVisitsReport } from '@/hooks/queries'
import { EmptyState, PageSpinner, Pagination, StatCard } from '@/components/ui'
import { fmt } from '@/utils/format'
import type { ReportPeriod, ReportQuery } from '@/types'

type Tab = 'revenue' | 'visits' | 'noShows' | 'arAging'
const TABS: Tab[] = ['revenue', 'visits', 'noShows', 'arAging']
const PERIODS: ReportPeriod[] = ['Day', 'Week', 'Month']
const KIND: Record<Tab, ReportKind> = { revenue: 'revenue', visits: 'visits', noShows: 'no-shows', arAging: 'ar-aging' }
const MAX_DAYS = 366
const DAY_MS = 86_400_000

// The API works in UTC calendar days, so the pickers do too.
const iso = (d: Date) => d.toISOString().slice(0, 10)
const daysAgo = (n: number) => iso(new Date(Date.now() - n * DAY_MS))
const spanDays = (from: string, to: string) => (Date.parse(to) - Date.parse(from)) / DAY_MS + 1
const isValidRange = (from: string, to: string) =>
  !!from && !!to && from <= to && spanDays(from, to) <= MAX_DAYS

const PRESETS = [{ key: 'd30', days: 29 }, { key: 'd90', days: 89 }, { key: 'y1', days: 364 }] as const
const COLOR = '#0891b2'
const COLOR_ALT = '#dc2626'

function ChartBox({ label, children }: { label: string; children: React.ReactElement }) {
  return (
    <div className="h-64" role="img" aria-label={label}>
      <ResponsiveContainer width="100%" height="100%">{children}</ResponsiveContainer>
    </div>
  )
}

export function ReportsPage() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('revenue')
  const [from, setFrom] = useState(daysAgo(29))
  const [to, setTo] = useState(daysAgo(0))
  const [period, setPeriod] = useState<ReportPeriod>('Day')
  const [arPage, setArPage] = useState(1)
  const [exporting, setExporting] = useState(false)

  const valid = isValidRange(from, to)
  const q: ReportQuery = { from, to, period }
  const ranged = tab !== 'arAging'

  const revenue = useRevenueReport(q, valid && tab === 'revenue')
  const visits = useVisitsReport(q, valid && tab === 'visits')
  const noShows = useNoShowReport(q, valid && tab === 'noShows')
  const ar = useArAgingReport(arPage, tab === 'arAging')

  const active = { revenue, visits, noShows, arAging: ar }[tab]

  const exportCsv = async () => {
    setExporting(true)
    try {
      const { blob, fileName } = await reportsApi.exportCsv(KIND[tab], ranged ? q : undefined)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = fileName
      a.click()
      URL.revokeObjectURL(url)
    } catch {
      toast.error(t('reports.exportFailed'))
    } finally {
      setExporting(false)
    }
  }

  const xTick = (v: string) => fmt.dateShort(v)
  const rate = (r: number | null) => (r == null ? t('reports.rateNA') : `${fmt.number(Math.round(r * 1000) / 10)}%`)

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{t('reports.title')}</h1>
          <p className="text-sm text-gray-500">{t('reports.scope')}</p>
        </div>
        <button type="button" className="btn-primary flex items-center gap-2" onClick={exportCsv}
          disabled={exporting || (ranged && !valid)}>
          <Download className="w-4 h-4" />{t('reports.exportCsv')}
        </button>
      </div>

      <div role="tablist" className="flex flex-wrap gap-2">
        {TABS.map(k => (
          <button key={k} type="button" role="tab" aria-selected={tab === k} onClick={() => setTab(k)}
            className={`px-3 py-1.5 rounded-lg text-sm font-medium ${tab === k ? 'bg-primary-600 text-white' : 'btn-ghost'}`}>
            {t(`reports.tabs.${k}`)}
          </button>
        ))}
      </div>

      {ranged ? (
        <div className="card p-4 space-y-3">
          <div className="flex flex-wrap gap-2">
            {PRESETS.map(p => (
              <button key={p.key} type="button" className="btn-ghost text-sm"
                onClick={() => { setFrom(daysAgo(p.days)); setTo(daysAgo(0)) }}>
                {t(`reports.presets.${p.key}`)}
              </button>
            ))}
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <div>
              <label className="label" htmlFor="report-from">{t('reports.from')}</label>
              <input id="report-from" type="date" className="input" value={from} onChange={e => setFrom(e.target.value)} />
            </div>
            <div>
              <label className="label" htmlFor="report-to">{t('reports.to')}</label>
              <input id="report-to" type="date" className="input" value={to} onChange={e => setTo(e.target.value)} />
            </div>
            <div>
              <label className="label" htmlFor="report-period">{t('reports.period')}</label>
              <select id="report-period" className="input" value={period} onChange={e => setPeriod(e.target.value as ReportPeriod)}>
                {PERIODS.map(p => <option key={p} value={p}>{t(`reports.periods.${p}`)}</option>)}
              </select>
            </div>
          </div>
          {!valid && <p role="alert" className="text-sm text-red-600">{t('reports.invalidRange')}</p>}
        </div>
      ) : (
        ar.data && <p className="text-sm text-gray-500">{t('reports.asOf', { date: fmt.date(ar.data.asOf) })}</p>
      )}

      {(ranged && !valid) ? null : active.isLoading ? <PageSpinner /> : active.isError ? (
        <p role="alert" className="text-sm text-red-600">{t('reports.loadFailed')}</p>
      ) : (
        <>
          {tab === 'revenue' && revenue.data && (
            <div className="space-y-4">
              <div className="flex flex-wrap gap-4">
                <StatCard icon="💵" label={t('reports.totalRevenue')} value={fmt.currency(revenue.data.totalRevenue)} />
                <StatCard icon="🧾" label={t('reports.invoicesPaid')} value={fmt.number(revenue.data.invoicesPaid)} />
              </div>
              <div className="card p-4">
                <p className="text-xs text-gray-400 mb-2">{t('reports.revenueNote')}</p>
                {revenue.data.series.length === 0 ? <p className="text-gray-400 text-sm">{t('reports.empty')}</p> : (
                  <ChartBox label={t('reports.tabs.revenue')}>
                    <BarChart data={revenue.data.series} margin={{ top: 5, right: 12, bottom: 5, left: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#9ca3af" strokeOpacity={0.3} />
                      <XAxis dataKey="periodStart" tickFormatter={xTick} tick={{ fontSize: 11 }} />
                      <YAxis width={60} tick={{ fontSize: 11 }} tickFormatter={(v: number) => fmt.number(v)} />
                      <Tooltip labelFormatter={(v: string) => fmt.date(v)}
                        formatter={(v: number) => [fmt.currency(v), t('reports.totalRevenue')]} />
                      <Bar dataKey="revenue" fill={COLOR} isAnimationActive={false} />
                    </BarChart>
                  </ChartBox>
                )}
              </div>
            </div>
          )}

          {tab === 'visits' && visits.data && (
            <div className="space-y-4">
              <div className="flex flex-wrap gap-4">
                <StatCard icon="🩺" label={t('reports.totalVisits')} value={fmt.number(visits.data.totalVisits)} />
              </div>
              <div className="card p-4">
                <p className="text-xs text-gray-400 mb-2">{t('reports.visitsNote')}</p>
                {visits.data.series.length === 0 ? <p className="text-gray-400 text-sm">{t('reports.empty')}</p> : (
                  <ChartBox label={t('reports.tabs.visits')}>
                    <BarChart data={visits.data.series} margin={{ top: 5, right: 12, bottom: 5, left: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#9ca3af" strokeOpacity={0.3} />
                      <XAxis dataKey="periodStart" tickFormatter={xTick} tick={{ fontSize: 11 }} />
                      <YAxis width={40} allowDecimals={false} tick={{ fontSize: 11 }} />
                      <Tooltip labelFormatter={(v: string) => fmt.date(v)}
                        formatter={(v: number) => [fmt.number(v), t('reports.totalVisits')]} />
                      <Bar dataKey="visits" fill={COLOR} isAnimationActive={false} />
                    </BarChart>
                  </ChartBox>
                )}
              </div>
            </div>
          )}

          {tab === 'noShows' && noShows.data && (
            <div className="space-y-4">
              <div className="flex flex-wrap gap-4">
                <StatCard icon="🚫" label={t('reports.noShowRate')} value={rate(noShows.data.rate)} color="text-red-600" />
                <StatCard icon="❌" label={t('reports.noShows')} value={fmt.number(noShows.data.noShows)} />
                <StatCard icon="✅" label={t('reports.completed')} value={fmt.number(noShows.data.completed)} />
              </div>
              <div className="card p-4">
                <p className="text-xs text-gray-400 mb-2">{t('reports.noShowNote')}</p>
                {noShows.data.series.length === 0 ? <p className="text-gray-400 text-sm">{t('reports.empty')}</p> : (
                  <ChartBox label={t('reports.noShowRate')}>
                    <LineChart data={noShows.data.series.map(p => ({ ...p, pct: p.rate == null ? null : Math.round(p.rate * 1000) / 10 }))}
                      margin={{ top: 5, right: 12, bottom: 5, left: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#9ca3af" strokeOpacity={0.3} />
                      <XAxis dataKey="periodStart" tickFormatter={xTick} tick={{ fontSize: 11 }} />
                      <YAxis width={44} domain={[0, 100]} unit="%" tick={{ fontSize: 11 }} />
                      <Tooltip labelFormatter={(v: string) => fmt.date(v)}
                        formatter={(v: number) => [`${fmt.number(v)}%`, t('reports.noShowRate')]} />
                      <Line type="monotone" dataKey="pct" stroke={COLOR_ALT} strokeWidth={2} dot={{ r: 3 }}
                        isAnimationActive={false} connectNulls />
                    </LineChart>
                  </ChartBox>
                )}
              </div>
            </div>
          )}

          {tab === 'arAging' && ar.data && (
            <div className="space-y-4">
              <div className="flex flex-wrap gap-4">
                <StatCard icon="💰" label={t('reports.totalOutstanding')} value={fmt.currency(ar.data.totalOutstanding)} color="text-red-600" />
                <StatCard icon="🧾" label={t('reports.openInvoices')} value={fmt.number(ar.data.openInvoices)} />
              </div>
              <div className="card p-4">
                <ChartBox label={t('reports.tabs.arAging')}>
                  <BarChart data={ar.data.buckets.map(b => ({ ...b, label: t(`reports.buckets.${b.bucket}`) }))}
                    margin={{ top: 5, right: 12, bottom: 5, left: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#9ca3af" strokeOpacity={0.3} />
                    <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                    <YAxis width={60} tick={{ fontSize: 11 }} tickFormatter={(v: number) => fmt.number(v)} />
                    <Tooltip formatter={(v: number) => [fmt.currency(v), t('reports.cols.balance')]} />
                    <Bar dataKey="amount" fill={COLOR_ALT} isAnimationActive={false} />
                  </BarChart>
                </ChartBox>
              </div>
              {ar.data.invoices.items.length === 0 ? (
                <div className="card"><EmptyState title={t('reports.empty')} /></div>
              ) : (
                <div className="card overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="text-left text-gray-500">
                        <th className="p-3">{t('reports.cols.invoice')}</th>
                        <th className="p-3">{t('reports.cols.patient')}</th>
                        <th className="p-3">{t('reports.cols.dueDate')}</th>
                        <th className="p-3">{t('reports.cols.daysPastDue')}</th>
                        <th className="p-3">{t('reports.cols.bucket')}</th>
                        <th className="p-3 text-right">{t('reports.cols.balance')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {ar.data.invoices.items.map(i => (
                        <tr key={i.invoiceId} className="border-t border-gray-100">
                          <td className="p-3 font-medium">{i.invoiceNumber}</td>
                          <td className="p-3">{i.patientName}</td>
                          <td className="p-3">{fmt.date(i.dueDate ?? i.invoiceDate)}</td>
                          <td className="p-3">{fmt.number(i.daysPastDue)}</td>
                          <td className="p-3">{t(`reports.buckets.${i.bucket}`)}</td>
                          <td className="p-3 text-right">{fmt.currency(i.balance)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <Pagination page={ar.data.invoices.page} totalPages={ar.data.invoices.totalPages} onPage={setArPage} />
                </div>
              )}
            </div>
          )}
        </>
      )}
    </div>
  )
}
