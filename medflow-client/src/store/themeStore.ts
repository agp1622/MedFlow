import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export type ThemeMode = 'light' | 'dark' | 'system'

interface ThemeState {
  mode: ThemeMode
  setMode: (mode: ThemeMode) => void
  /** The resolved theme (light or dark) based on mode + system preference */
  resolvedTheme: 'light' | 'dark'
  /** Called internally to sync resolved theme with system changes */
  _syncResolved: () => void
}

function getSystemTheme(): 'light' | 'dark' {
  if (typeof window === 'undefined') return 'light'
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function resolveTheme(mode: ThemeMode): 'light' | 'dark' {
  return mode === 'system' ? getSystemTheme() : mode
}

function applyThemeToDOM(resolved: 'light' | 'dark') {
  const root = document.documentElement
  if (resolved === 'dark') {
    root.classList.add('dark')
  } else {
    root.classList.remove('dark')
  }
}

export const useThemeStore = create<ThemeState>()(
  persist(
    (set, get) => ({
      mode: 'system' as ThemeMode,
      resolvedTheme: resolveTheme('system'),

      setMode: (mode: ThemeMode) => {
        const resolved = resolveTheme(mode)
        applyThemeToDOM(resolved)
        set({ mode, resolvedTheme: resolved })
      },

      _syncResolved: () => {
        const { mode } = get()
        const resolved = resolveTheme(mode)
        applyThemeToDOM(resolved)
        set({ resolvedTheme: resolved })
      },
    }),
    {
      name: 'medflow-theme',
      partialize: (state) => ({ mode: state.mode }),
      onRehydrateStorage: () => (state) => {
        if (state) {
          const resolved = resolveTheme(state.mode)
          applyThemeToDOM(resolved)
          state.resolvedTheme = resolved
        }
      },
    }
  )
)

// Listen for system theme changes to update in real-time when mode is 'system'
if (typeof window !== 'undefined') {
  const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
  mediaQuery.addEventListener('change', () => {
    useThemeStore.getState()._syncResolved()
  })

  // Apply on initial load
  const { mode } = useThemeStore.getState()
  applyThemeToDOM(resolveTheme(mode))
}
