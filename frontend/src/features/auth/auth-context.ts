import { createContext } from 'react'
import type { CurrentUser } from './types'

/** Lifecycle of the auth session; `loading` is the boot window while we try a silent refresh. */
export type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'

export interface AuthContextValue {
  user: CurrentUser | null
  status: AuthStatus
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
