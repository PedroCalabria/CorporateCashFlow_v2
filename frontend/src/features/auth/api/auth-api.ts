import { apiClient } from '@/lib/api-client'
import { setAccessToken } from '../token-store'
import type { CurrentUser, LoginResponse } from '../types'

/**
 * Auth API calls. Each stores the returned access token in the in-memory store; the
 * refresh cookie is handled by the browser (HttpOnly) via `withCredentials` on the client.
 */

export async function login(email: string, password: string): Promise<string> {
  const { data } = await apiClient.post<LoginResponse>('/auth/login', { email, password })
  setAccessToken(data.accessToken)
  return data.accessToken
}

/**
 * Attempt to restore a session from the HttpOnly refresh cookie. Marked `skipAuthRefresh`
 * so a 401 here does not recurse through the interceptor's refresh path.
 */
export async function refresh(): Promise<string> {
  const { data } = await apiClient.post<LoginResponse>('/auth/refresh', undefined, {
    skipAuthRefresh: true,
  } as never)
  setAccessToken(data.accessToken)
  return data.accessToken
}

export async function getCurrentUser(): Promise<CurrentUser> {
  const { data } = await apiClient.get<CurrentUser>('/auth/me')
  return data
}

export async function logout(): Promise<void> {
  await apiClient.post('/auth/logout')
}
