import { apiClient, refreshAccessToken } from '@/lib/api-client'
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
 * Attempt to restore a session from the HttpOnly refresh cookie. Delegates to the api-client's
 * single-flight `refreshAccessToken` so this boot-time call and any concurrent 401-driven refresh
 * share ONE in-flight request — critical because React StrictMode invokes the boot effect twice;
 * without sharing, two concurrent refreshes carry the same cookie, the server rotates it on the
 * first and rejects (and clears the cookie for) the second, logging the user out on reload.
 */
export function refresh(): Promise<string> {
  return refreshAccessToken()
}

export async function getCurrentUser(): Promise<CurrentUser> {
  const { data } = await apiClient.get<CurrentUser>('/auth/me')
  return data
}

export async function logout(): Promise<void> {
  await apiClient.post('/auth/logout')
}
