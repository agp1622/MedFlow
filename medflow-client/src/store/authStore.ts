import { create } from 'zustand'
import type { UserDto } from '@/types'

interface AuthState {
  user: UserDto | null
  token: string | null
  isAuthenticated: boolean
  login: (token: string, user: UserDto) => void
  logout: () => void
}

const storedUser = localStorage.getItem('medflow_user')
const storedToken = localStorage.getItem('medflow_token')

export const useAuthStore = create<AuthState>((set) => ({
  user: storedUser ? JSON.parse(storedUser) : null,
  token: storedToken,
  isAuthenticated: !!storedToken,

  login: (token, user) => {
    localStorage.setItem('medflow_token', token)
    localStorage.setItem('medflow_user', JSON.stringify(user))
    set({ token, user, isAuthenticated: true })
  },

  logout: () => {
    localStorage.removeItem('medflow_token')
    localStorage.removeItem('medflow_user')
    set({ token: null, user: null, isAuthenticated: false })
  },
}))
