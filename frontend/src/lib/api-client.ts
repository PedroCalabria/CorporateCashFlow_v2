import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { emitForcedLogout } from '@/features/auth/auth-events'
import { clearAccessToken, getAccessToken, setAccessToken } from '@/features/auth/token-store'

/** Extra flags we hang off a request config to control interceptor behaviour. */
interface AuthRequestConfig extends InternalAxiosRequestConfig {
  /** True once we've already retried this request after a refresh — prevents infinite loops. */
  _retried?: boolean
  /** True for the refresh call itself, so a 401 there does not trigger another refresh. */
  skipAuthRefresh?: boolean
}

/**
 * Single axios instance for the whole app. Same-origin `/api` (proxied to the backend in
 * dev) with `withCredentials` so the HttpOnly refresh cookie rides along. Interceptors add
 * the in-memory access token and transparently renew it on a `401` (design.md §D5).
 */
export const apiClient = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

// Request: attach the in-memory access token (except on the refresh call).
apiClient.interceptors.request.use((config: AuthRequestConfig) => {
  const token = getAccessToken()
  if (token && !config.skipAuthRefresh) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// De-duplicate concurrent refreshes: all 401s share the one in-flight refresh promise.
let refreshPromise: Promise<string> | null = null

export function refreshAccessToken(): Promise<string> {
  if (!refreshPromise) {
    refreshPromise = apiClient
      .post<{ accessToken: string }>('/auth/refresh', undefined, {
        skipAuthRefresh: true,
      } as AuthRequestConfig)
      .then((response) => {
        const token = response.data.accessToken
        setAccessToken(token)
        return token
      })
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

// Response: on a 401 caused by an expired access token, refresh once and retry.
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as AuthRequestConfig | undefined

    const shouldTryRefresh =
      error.response?.status === 401 &&
      config !== undefined &&
      !config._retried &&
      !config.skipAuthRefresh

    if (!shouldTryRefresh || config === undefined) {
      return Promise.reject(error)
    }

    try {
      const token = await refreshAccessToken()
      config._retried = true
      config.headers.Authorization = `Bearer ${token}`
      return apiClient(config)
    } catch (refreshError) {
      // Refresh failed (revoked/expired) — drop the session and let the guard redirect.
      clearAccessToken()
      emitForcedLogout()
      return Promise.reject(refreshError)
    }
  },
)
