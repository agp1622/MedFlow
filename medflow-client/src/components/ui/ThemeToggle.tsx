import { Sun, Moon, Monitor } from 'lucide-react'
import { useThemeStore, ThemeMode } from '@/store/themeStore'
import { useState, useRef, useEffect } from 'react'

const OPTIONS: { mode: ThemeMode; icon: typeof Sun; label: string }[] = [
  { mode: 'light',  icon: Sun,     label: 'Light' },
  { mode: 'dark',   icon: Moon,    label: 'Dark' },
  { mode: 'system', icon: Monitor, label: 'System' },
]

export function ThemeToggle() {
  const { mode, setMode, resolvedTheme } = useThemeStore()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  // Close dropdown on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  const ActiveIcon = resolvedTheme === 'dark' ? Moon : Sun

  return (
    <div className="relative" ref={ref}>
      <button
        id="theme-toggle-button"
        onClick={() => setOpen(!open)}
        aria-label="Toggle theme"
        className="
          group relative w-8 h-8 rounded-lg
          flex items-center justify-center
          bg-navy-600/50 hover:bg-navy-600
          transition-all duration-200
          focus:outline-none focus:ring-2 focus:ring-primary-400/40
        "
      >
        <ActiveIcon
          size={15}
          className="text-[#7A9BB5] group-hover:text-primary-400 transition-colors duration-200"
        />

        {/* Subtle glow effect on hover */}
        <span className="
          absolute inset-0 rounded-lg
          bg-primary-400/0 group-hover:bg-primary-400/10
          transition-all duration-300
          pointer-events-none
        " />
      </button>

      {/* Dropdown */}
      {open && (
        <div
          className="
            absolute bottom-full left-0 mb-2
            w-[140px] py-1.5
            bg-navy-700 border border-navy-600
            rounded-xl shadow-modal
            animate-in fade-in slide-in-from-bottom-2
            z-50
          "
          style={{
            animation: 'themeDropdownIn 150ms ease-out',
          }}
        >
          {OPTIONS.map(({ mode: m, icon: Icon, label }) => {
            const isActive = mode === m
            return (
              <button
                key={m}
                id={`theme-option-${m}`}
                onClick={() => { setMode(m); setOpen(false) }}
                className={`
                  w-full flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium
                  transition-all duration-150
                  ${isActive
                    ? 'text-primary-400 bg-navy-600/70'
                    : 'text-[#7A9BB5] hover:text-white hover:bg-navy-600/40'
                  }
                `}
              >
                <Icon size={13} />
                {label}
                {isActive && (
                  <span className="ml-auto w-1.5 h-1.5 rounded-full bg-primary-400" />
                )}
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
