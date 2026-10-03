import { format, formatDistanceToNow, parseISO, isToday, isTomorrow } from 'date-fns'
import { es as esLocale, enUS } from 'date-fns/locale'
import type { Locale } from 'date-fns'
import type { ParseKeys } from 'i18next'
import i18n, { currentLanguage, DEFAULT_LANGUAGE } from '@/i18n'

// Per-language formatting conventions. Registering a new language adds an entry here.
interface LocaleConfig { dateFns: Locale; intl: string; shortDate: string }
const LOCALES: Record<string, LocaleConfig> = {
  es: { dateFns: esLocale, intl: 'es', shortDate: 'd MMM' },
  en: { dateFns: enUS, intl: 'en-US', shortDate: 'MMM d' },
}
const localeConfig = (): LocaleConfig => LOCALES[currentLanguage()] ?? LOCALES[DEFAULT_LANGUAGE]

// Currency stays US dollars; only the presentation follows the language
const CURRENCY = 'USD'

export const fmt = {
  date: (d?: string | null) => d ? format(parseISO(d), 'PP', { locale: localeConfig().dateFns }) : '—',
  dateTime: (d?: string | null) => d ? format(parseISO(d), 'PP p', { locale: localeConfig().dateFns }) : '—',
  time: (d?: string | null) => d ? format(parseISO(d), 'p', { locale: localeConfig().dateFns }) : '—',
  relative: (d?: string | null) => d ? formatDistanceToNow(parseISO(d), { addSuffix: true, locale: localeConfig().dateFns }) : '—',
  number: (n?: number | null) => n != null ? new Intl.NumberFormat(localeConfig().intl).format(n) : '—',
  currency: (n?: number | null) => n != null
    ? new Intl.NumberFormat(localeConfig().intl, { style: 'currency', currency: CURRENCY }).format(n)
    : '—',
  dateShort: (d?: string | null) => {
    if (!d) return '—'
    const date = parseISO(d)
    if (isToday(date)) return i18n.t('common.today')
    if (isTomorrow(date)) return i18n.t('common.tomorrow')
    const cfg = localeConfig()
    return format(date, cfg.shortDate, { locale: cfg.dateFns })
  },
  /** Date-time for ISO strings that carry an offset (audit timestamps). */
  timestamp: (d?: string | null) => d ? format(new Date(d), 'PP p', { locale: localeConfig().dateFns }) : '—',
}

type StatusVariant = 'green' | 'yellow' | 'red' | 'gray' | 'blue'

const STATUS_COLORS: Record<string, StatusVariant> = {
  Active: 'green', Confirmed: 'green', Paid: 'green', Completed: 'green',
  Pending: 'yellow', ExpiringSoon: 'yellow',
  Overdue: 'red', Cancelled: 'red', NoShow: 'red', Expired: 'red', Deceased: 'red',
  Inactive: 'gray', Draft: 'gray',
  NewPatient: 'blue', Emergency: 'red',
}

const VARIANT_CLASSES: Record<StatusVariant, string> = {
  green:  'bg-emerald-50 text-emerald-700',
  yellow: 'bg-amber-50 text-amber-700',
  red:    'bg-red-50 text-red-700',
  gray:   'bg-gray-100 text-gray-600',
  blue:   'bg-blue-50 text-blue-700',
}

export const statusClass = (status: string) =>
  VARIANT_CLASSES[STATUS_COLORS[status] ?? 'gray']

/** Label for a system-defined value (status, type, weekday). Stored values are never changed. */
export const displayEnum = (s: string) =>
  i18n.t(`enums.${s}` as ParseKeys, { defaultValue: s.replace(/([A-Z])/g, ' $1').trim() })

// Blood group notation is the same in every language
export const bloodTypeDisplay: Record<string, string> = {
  APos: 'A+', ANeg: 'A−', BPos: 'B+', BNeg: 'B−',
  ABPos: 'AB+', ABNeg: 'AB−', OPos: 'O+', ONeg: 'O−', Unknown: '?'
}

export const initials = (name: string) =>
  name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase()

const AVATAR_PALETTE = [
  'bg-cyan-100 text-cyan-700', 'bg-emerald-100 text-emerald-700',
  'bg-amber-100 text-amber-700', 'bg-violet-100 text-violet-700',
  'bg-rose-100 text-rose-700', 'bg-sky-100 text-sky-700',
]
export const avatarColor = (name: string) =>
  AVATAR_PALETTE[name.charCodeAt(0) % AVATAR_PALETTE.length]
