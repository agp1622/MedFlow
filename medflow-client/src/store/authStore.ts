import { create } from 'zustand'
import type { UserDto } from '@/types'

interface AuthState {
  user: UserDto | null
  token: string | null
  isAuthenticated: boolean
  login: (token: string, user: UserDto) => void
  /** Refreshes role/clinic from the API (a role can change while a token is still valid) */
  updateUser: (patch: Partial<UserDto>) => void
  logout: () => void
}

// A session saved before roles existed has no role, and the API now rejects such tokens (403),
// so drop it and make the user sign in again.
const parsedUser: UserDto | null = (() => {
  try { return JSON.parse(localStorage.getItem('medflow_user') ?? 'null') } catch { return null }
})()
const hasValidSession = !!parsedUser?.role && !!localStorage.getItem('medflow_token')
if (!hasValidSession) {
  localStorage.removeItem('medflow_user')
  localStorage.removeItem('medflow_token')
}
const storedUser = hasValidSession ? JSON.stringify(parsedUser) : null
const storedToken = hasValidSession ? localStorage.getItem('medflow_token') : null

export const useAuthStore = create<AuthState>((set) => ({
  user: storedUser ? JSON.parse(storedUser) : null,
  token: storedToken,
  isAuthenticated: !!storedToken,

  login: (token, user) => {
    localStorage.setItem('medflow_token', token)
    localStorage.setItem('medflow_user', JSON.stringify(user))
    set({ token, user, isAuthenticated: true })
  },

  updateUser: (patch) => set((state) => {
    if (!state.user) return state
    const user = { ...state.user, ...patch }
    localStorage.setItem('medflow_user', JSON.stringify(user))
    return { user }
  }),

  logout: () => {
    localStorage.removeItem('medflow_token')
    localStorage.removeItem('medflow_user')
    set({ token: null, user: null, isAuthenticated: false })
  },
}))
