import { Eye, EyeOff } from 'lucide-react'

interface ShareToggleProps {
  shared: boolean
  onChange: (shared: boolean) => void
  disabled?: boolean
}

/** Doctor-side switch controlling whether an item is visible in the patient portal. */
export function ShareToggle({ shared, onChange, disabled }: ShareToggleProps) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={shared}
      disabled={disabled}
      onClick={() => onChange(!shared)}
      title={shared ? 'Visible to the patient in their portal. Click to stop sharing.' : 'Hidden from the patient. Click to share.'}
      className={`inline-flex items-center gap-1.5 px-2 py-1 rounded-full text-xs font-medium transition-colors disabled:opacity-50 ${
        shared ? 'bg-emerald-50 text-emerald-700 hover:bg-emerald-100' : 'bg-gray-100 text-gray-500 hover:bg-gray-200'
      }`}
    >
      {shared ? <Eye size={12} /> : <EyeOff size={12} />}
      {shared ? 'Shared with patient' : 'Not shared'}
    </button>
  )
}
