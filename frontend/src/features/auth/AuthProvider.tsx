import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import * as authApi from './api/auth-api'
import { AuthContext, type AuthStatus } from './auth-context'
import { onForcedLogout } from './auth-events'
import { clearAccessToken } from './token-store'
import type { CurrentUser } from './types'

/**
 * Owns the auth session state (user + status) and exposes login/logout. On mount it
 * attempts a silent `/auth/refresh` to restore a session from an existing HttpOnly cookie
 * (design.md §D5); while that runs, status is `loading` so the route guard neither renders
 * the shell nor bounces to login prematurely. It also listens for a forced logout emitted
 * by the axios interceptor when a refresh fails mid-session.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [status, setStatus] = useState<AuthStatus>('loading')

  // Boot: try to restore an existing session.
  useEffect(() => {
    let active = true

    async function restore() {
      try {
        await authApi.refresh()
        const currentUser = await authApi.getCurrentUser()
        if (active) {
          setUser(currentUser)
          setStatus('authenticated')
        }
      } catch {
        if (active) {
          clearAccessToken()
          setUser(null)
          setStatus('unauthenticated')
        }
      }
    }

    void restore()
    return () => {
      active = false
    }
  }, [])

  // The interceptor cleared the token after a failed refresh — reflect it in React state.
  useEffect(
    () =>
      onForcedLogout(() => {
        setUser(null)
        setStatus('unauthenticated')
      }),
    [],
  )

  const login = useCallback(async (email: string, password: string) => {
    await authApi.login(email, password)
    const currentUser = await authApi.getCurrentUser()
    setUser(currentUser)
    setStatus('authenticated')
  }, [])

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } finally {
      clearAccessToken()
      setUser(null)
      setStatus('unauthenticated')
    }
  }, [])

  const value = useMemo(
    () => ({
      user,
      status,
      isAuthenticated: status === 'authenticated',
      login,
      logout,
    }),
    [user, status, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
