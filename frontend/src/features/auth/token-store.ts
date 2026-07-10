/**
 * In-memory access-token store. The token lives in a module-level variable ONLY — never
 * in `localStorage`/`sessionStorage` — to reduce XSS exposure (spec: "in-memory token").
 * It is deliberately outside React state so the axios interceptor (non-React code) can
 * read it synchronously on every request. It is lost on a full page reload; the session is
 * then restored via a silent `/auth/refresh` using the HttpOnly cookie.
 */
let accessToken: string | null = null

export function getAccessToken(): string | null {
  return accessToken
}

export function setAccessToken(token: string | null): void {
  accessToken = token
}

export function clearAccessToken(): void {
  accessToken = null
}
