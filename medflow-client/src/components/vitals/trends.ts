import type { VitalSignDto } from '@/types'

export type VitalKey = 'bloodPressure' | 'heartRate' | 'weight' | 'bmi' | 'temperature' | 'oxygen'

export interface TrendPoint { t: number; [series: string]: number }

/** yyyy-mm-dd strings from <input type="date">; either side may be empty (open-ended). */
export interface DateRange { from?: string; to?: string }

export type RangePreset = 'd30' | 'm6' | 'y1' | 'all'

/** The API serialises UTC timestamps without a zone designator; treat those as UTC. */
export function toEpoch(iso: string): number {
  const hasZone = /([zZ]|[+-]\d{2}:?\d{2})$/.test(iso)
  return new Date(hasZone ? iso : `${iso}Z`).getTime()
}

/** "120/80" -> { systolic: 120, diastolic: 80 }; anything else -> null. */
export function parseBloodPressure(value?: string | null): { systolic: number; diastolic: number } | null {
  const m = /^\s*(\d{2,3})\s*\/\s*(\d{2,3})\s*$/.exec(value ?? '')
  return m ? { systolic: Number(m[1]), diastolic: Number(m[2]) } : null
}

const pad = (n: number) => String(n).padStart(2, '0')
const toInputDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

export function rangeFromPreset(preset: RangePreset, now: Date = new Date()): DateRange {
  if (preset === 'all') return {}
  const from = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  if (preset === 'd30') from.setDate(from.getDate() - 30)
  else if (preset === 'm6') from.setMonth(from.getMonth() - 6)
  else from.setFullYear(from.getFullYear() - 1)
  return { from: toInputDate(from) }
}

export function isValidRange(r: DateRange): boolean {
  return !(r.from && r.to && r.to < r.from)
}

/** Inclusive of both boundary days, in the viewer's local calendar. */
export function filterByRange(vitals: VitalSignDto[], r: DateRange): VitalSignDto[] {
  if (!isValidRange(r)) return vitals
  const start = r.from ? new Date(`${r.from}T00:00:00`).getTime() : -Infinity
  const end = r.to ? new Date(`${r.to}T23:59:59.999`).getTime() : Infinity
  return vitals.filter(v => {
    const t = toEpoch(v.recordedAt)
    return t >= start && t <= end
  })
}

/** Chronological (oldest first) points for one vital; records without a value are skipped. */
export function buildSeries(vitals: VitalSignDto[], key: VitalKey): TrendPoint[] {
  const pts: TrendPoint[] = []
  for (const v of vitals) {
    const t = toEpoch(v.recordedAt)
    if (Number.isNaN(t)) continue
    if (key === 'bloodPressure') {
      const bp = parseBloodPressure(v.bloodPressure)
      if (bp) pts.push({ t, systolic: bp.systolic, diastolic: bp.diastolic })
      continue
    }
    const value = key === 'oxygen' ? v.oxygenSaturation : v[key]
    if (value != null) pts.push({ t, value })
  }
  return pts.sort((a, b) => a.t - b.t)
}
