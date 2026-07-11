/** The three roles, matching the backend `UserRole` enum names. */
export type Role = 'Manager' | 'Editor' | 'Auditor'

/** A user as returned by the user-management endpoints. Never carries a password hash. */
export interface User {
  id: string
  name: string
  email: string
  role: Role
  /** null = global scope; otherwise the subsidiary this user is tied to. */
  subsidiaryId: string | null
  isActive: boolean
}

/** Body for creating a user — no password (the backend generates it and returns it once). */
export interface CreateUserInput {
  name: string
  email: string
  role: Role
  subsidiaryId: string | null
}

/** Create response — the user plus the one-time initial password (shown once). */
export interface CreateUserResult {
  user: User
  initialPassword: string
}

/** Body for editing a user — role/scope only. */
export interface UpdateUserInput {
  role: Role
  subsidiaryId: string | null
}

/** Body for a Manager-set password reset. */
export interface ResetPasswordInput {
  newPassword: string
}
