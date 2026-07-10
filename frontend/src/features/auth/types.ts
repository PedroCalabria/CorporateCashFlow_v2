/** The authenticated user as returned by `GET /api/auth/me`. */
export interface CurrentUser {
  id: string
  name: string
  email: string
  role: 'Manager' | 'Editor' | 'Auditor'
  /** null = global scope; otherwise the subsidiary this user is tied to. */
  subsidiaryId: string | null
}

/** Login/refresh response body — only the access token travels here. */
export interface LoginResponse {
  accessToken: string
}
