import { avatarColor, initials, statusClass, displayEnum } from '@/utils/format'
import { Loader2 } from 'lucide-react'

// ── Badge ─────────────────────────────────────────────────────────────────────
export function Badge({ status }: { status: string }) {
  return (
    <span className={`badge ${statusClass(status)}`}>
      {displayEnum(status)}
    </span>
  )
}

// ── Avatar ────────────────────────────────────────────────────────────────────
interface AvatarProps { name: string; size?: 'sm' | 'md' | 'lg' }
export function Avatar({ name, size = 'md' }: AvatarProps) {
  const sizes = { sm: 'w-7 h-7 text-xs', md: 'w-9 h-9 text-sm', lg: 'w-14 h-14 text-xl' }
  return (
    <div className={`${sizes[size]} ${avatarColor(name)} rounded-full flex items-center justify-center font-bold flex-shrink-0`}>
      {initials(name)}
    </div>
  )
}

// ── Spinner ───────────────────────────────────────────────────────────────────
export function Spinner({ className = '' }: { className?: string }) {
  return <Loader2 className={`animate-spin text-primary-600 ${className}`} />
}

export function PageSpinner() {
  return (
    <div className="flex-1 flex items-center justify-center min-h-64">
      <Spinner className="w-8 h-8" />
    </div>
  )
}

// ── EmptyState ────────────────────────────────────────────────────────────────
interface EmptyStateProps { title: string; description?: string; action?: React.ReactNode }
export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <div className="w-12 h-12 bg-gray-100 rounded-full flex items-center justify-center mb-4">
        <span className="text-2xl">📋</span>
      </div>
      <p className="font-semibold text-gray-700">{title}</p>
      {description && <p className="text-gray-400 text-sm mt-1">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}

// ── Pagination ────────────────────────────────────────────────────────────────
interface PaginationProps { page: number; totalPages: number; onPage: (p: number) => void }
export function Pagination({ page, totalPages, onPage }: PaginationProps) {
  if (totalPages <= 1) return null
  return (
    <div className="flex items-center justify-center gap-1 py-4">
      <button className="btn-ghost px-3 py-1.5 text-sm" disabled={page === 1} onClick={() => onPage(page - 1)}>← Prev</button>
      {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => i + 1).map(p => (
        <button key={p} onClick={() => onPage(p)}
          className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-colors ${p === page ? 'bg-primary-600 text-white' : 'text-gray-500 hover:bg-gray-100'}`}>
          {p}
        </button>
      ))}
      <button className="btn-ghost px-3 py-1.5 text-sm" disabled={page === totalPages} onClick={() => onPage(page + 1)}>Next →</button>
    </div>
  )
}

// ── StatCard ──────────────────────────────────────────────────────────────────
interface StatCardProps { icon: string; label: string; value: string | number; sub?: string; color?: string }
export function StatCard({ icon, label, value, sub, color = 'text-primary-600' }: StatCardProps) {
  return (
    <div className="card p-5 flex items-center gap-4 flex-1 min-w-44">
      <div className="w-12 h-12 rounded-xl bg-gray-50 flex items-center justify-center text-2xl flex-shrink-0">{icon}</div>
      <div>
        <div className="text-2xl font-bold text-gray-900 leading-none">{value}</div>
        <div className="text-xs text-gray-500 mt-1">{label}</div>
        {sub && <div className={`text-xs font-semibold mt-0.5 ${color}`}>{sub}</div>}
      </div>
    </div>
  )
}

// ── SearchInput ───────────────────────────────────────────────────────────────
interface SearchInputProps { value: string; onChange: (v: string) => void; placeholder?: string }
export function SearchInput({ value, onChange, placeholder = 'Search...' }: SearchInputProps) {
  return (
    <div className="relative">
      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm">🔍</span>
      <input className="input pl-8 pr-3 h-9 w-56" placeholder={placeholder}
        value={value} onChange={e => onChange(e.target.value)} />
    </div>
  )
}
