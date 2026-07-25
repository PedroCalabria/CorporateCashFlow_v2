import type { CurrentUser } from './types'

/**
 * True only for the Global Manager: the `Manager` role with no subsidiary scope. This mirrors
 * the backend's Global-Manager authorization (a `Manager` with a null `subsidiaryId`) and drives
 * client-side visibility/route guards. The backend remains the source of truth — this is a UX
 * convenience, not a security boundary.
 */
export function isGlobalManager(user: CurrentUser | null): boolean {
  return user?.role === 'Manager' && user.subsidiaryId === null
}

/**
 * True for any Manager, global or subsidiary-scoped. Drives visibility of the Manager-only
 * user-management screen; the backend enforces the same coarse gate (403 for Editor/Auditor).
 */
export function isManager(user: CurrentUser | null): boolean {
  return user?.role === 'Manager'
}

/**
 * True for a Manager or Auditor (global or subsidiary-scoped). Drives visibility of the
 * audit-trail screen — an Editor has no access to this capability (docs/requirements-document.md
 * §2). The backend enforces the same gate (403 for Editor) on both `/api/audit-log` and
 * `/api/access-log`.
 */
export function isManagerOrAuditor(user: CurrentUser | null): boolean {
  return user?.role === 'Manager' || user?.role === 'Auditor'
}
