import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { usePatientVitals } from '@/hooks/queries'
import { PageSpinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import {
  buildSeries, filterByRange, isValidRange, rangeFromPreset,
  type DateRange, type RangePreset, type VitalKey,
} from './trends'

type Preset = RangePreset | 'custom'
const PRESETS: Preset[] = ['d30', 'm6', 'y1', 'all', 'custom']

const CHARTS: { key: VitalKey; unit: string; lines: { dataKey: string; color: string; nameKey?: 'systolic' | 'diastolic' }[] }[] = [
  { key: 'bloodPressure', unit: 'mmHg', lines: [
    { dataKey: 'systolic', color: '#dc2626', nameKey: 'systolic' },
    { dataKey: 'diastolic', color: '#2563eb', nameKey: 'diastolic' },
  ] },
  { key: 'heartRate', unit: 'bpm', lines: [{ dataKey: 'value', color: '#059669' }] },
  { key: 'weight', unit: 'kg', lines: [{ dataKey: 'value', color: '#7c3aed' }] },
  { key: 'bmi', unit: 'kg/m²', lines: [{ dataKey: 'value', color: '#d97706' }] },
  { key: 'temperature', unit: '°C', lines: [{ dataKey: 'value', color: '#db2777' }] },
  { key: 'oxygen', unit: '%', lines: [{ dataKey: 'value', color: '#0891b2' }] },
]

export function VitalsTrends({ patientId }: { patientId: number }) {
  const { t } = useTranslation()
  const { data: vitals, isLoading } = usePatientVitals(patientId)
  const [preset, setPreset] = useState<Preset>('all')
  const [custom, setCustom] = useState<DateRange>({})

  const range: DateRange = preset === 'custom' ? custom : rangeFromPreset(preset)
  const valid = isValidRange(range)
  // An invalid custom range keeps the previously valid charts instead of rendering nothing.
  const [lastValid, setLastValid] = useState<DateRange>({})
  const effective = valid ? range : lastValid
  if (valid && (effective.from !== lastValid.from || effective.to !== lastValid.to)) setLastValid(effective)

  const { from, to } = effective
  const filtered = useMemo(() => filterByRange(vitals ?? [], { from, to }), [vitals, from, to])
  const charts = useMemo(
    () => CHARTS.map(c => ({ ...c, data: buildSeries(filtered, c.key) })).filter(c => c.data.length > 0),
    [filtered],
  )

  if (isLoading) return <PageSpinner />

  return (
    <div className="space-y-5">
      <div className="card p-4 sm:p-5 space-y-3">
        <h3 className="font-bold text-gray-800">{t('patients.trends.title')}</h3>
        <div className="flex flex-wrap gap-2">
          {PRESETS.map(p => (
            <button key={p} type="button" onClick={() => setPreset(p)}
              className={`px-3 py-1.5 rounded-lg text-sm font-medium ${preset === p ? 'bg-primary-600 text-white' : 'btn-ghost'}`}>
              {t(`patients.trends.range.${p}`)}
            </button>
          ))}
        </div>
        {preset === 'custom' && (
          <div className="flex flex-wrap items-end gap-3">
            <div>
              <label className="label" htmlFor="trends-from">{t('patients.trends.from')}</label>
              <input id="trends-from" type="date" className="input" value={custom.from ?? ''}
                onChange={e => setCustom(c => ({ ...c, from: e.target.value || undefined }))} />
            </div>
            <div>
              <label className="label" htmlFor="trends-to">{t('patients.trends.to')}</label>
              <input id="trends-to" type="date" className="input" value={custom.to ?? ''}
                onChange={e => setCustom(c => ({ ...c, to: e.target.value || undefined }))} />
            </div>
          </div>
        )}
        {!valid && <p role="alert" className="text-sm text-red-600">{t('patients.trends.invalidRange')}</p>}
      </div>

      {!vitals?.length ? (
        <div className="card p-5"><p className="text-gray-400 text-sm">{t('patients.vitals.none')}</p></div>
      ) : charts.length === 0 ? (
        <div className="card p-5"><p className="text-gray-400 text-sm">{t('patients.trends.noneInRange')}</p></div>
      ) : (
        <div className="grid grid-cols-1 xl:grid-cols-2 gap-5">
          {charts.map(c => (
            <div key={c.key} className="card p-4 sm:p-5">
              <h4 className="font-semibold text-gray-800 mb-3">
                {t(`patients.vitals.${c.key}`)} <span className="text-xs font-normal text-gray-400">({c.unit})</span>
              </h4>
              <div className="h-56" role="img" aria-label={t(`patients.vitals.${c.key}`)}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={c.data} margin={{ top: 5, right: 12, bottom: 5, left: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#9ca3af" strokeOpacity={0.3} />
                    <XAxis dataKey="t" type="number" scale="time" domain={['dataMin', 'dataMax']}
                      tickFormatter={(v: number) => fmt.dateShort(new Date(v).toISOString())} tick={{ fontSize: 11 }} />
                    <YAxis domain={['auto', 'auto']} width={44} tick={{ fontSize: 11 }} />
                    <Tooltip labelFormatter={(v: number) => fmt.timestamp(new Date(v).toISOString())}
                      formatter={(val: number, name: string) => [`${fmt.number(val)} ${c.unit}`, name]} />
                    {c.lines.length > 1 && <Legend />}
                    {c.lines.map(l => (
                      <Line key={l.dataKey} type="monotone" dataKey={l.dataKey} stroke={l.color}
                        name={l.nameKey ? t(`patients.trends.${l.nameKey}`) : t(`patients.vitals.${c.key}`)}
                        strokeWidth={2} dot={{ r: 3 }} isAnimationActive={false} connectNulls />
                    ))}
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
