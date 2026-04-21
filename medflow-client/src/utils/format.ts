import { format, formatDistanceToNow, parseISO, isToday, isTomorrow } from 'date-fns'

export const fmt = {
  date: (d?: string | null) => d ? format(parseISO(d), 'MMM d, yyyy') : '—',
  dateTime: (d?: string | null) => d ? format(parseISO(d), 'MMM d, yyyy h:mm a') : '—',
  time: (d?: string | null) => d ? format(parseISO(d), 'h:mm a') : '—',
  relative: (d?: string | null) => d ? formatDistanceToNow(parseISO(d), { addSuffix: true }) : '—',
  currency: (n?: number | null) => n != null ? `$${n.toFixed(2)}` : '—',
  dateShort: (d?: string | null) => {
    if (!d) return '—'
    const date = parseISO(d)
    if (isToday(date)) return 'Today'
    if (isTomorrow(date)) return 'Tomorrow'
    return format(date, 'MMM d')
  },
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

export const displayEnum = (s: string) =>
  s.replace(/([A-Z])/g, ' $1').trim()

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
