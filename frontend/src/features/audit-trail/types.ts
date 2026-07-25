export type AuditAction =
  | 'Created'
  | 'Updated'
  | 'JustificationSubmitted'
  | 'Approved'
  | 'Rejected'
  | 'Reverted'
  | 'Deleted'

/** A row from the "Ledger Activity" tab (GET /api/audit-log). `performedByName` is resolved server-side for display ("System" for the automatic-match actor). */
export interface AuditLogEntry {
  id: string
  entityType: string
  entityId: string
  action: AuditAction
  performedBy: string
  performedByName: string
  performedAt: string
  oldValue: string | null
  newValue: string | null
}

export type AccessLogEventType = 'LoginSuccess' | 'LoginFailed' | 'AccessDenied'

/** A row from the "Access Activity" tab (GET /api/access-log). `userId`/`userName` are null for a failed login against an unknown email. */
export interface AccessLogEntry {
  id: string
  userId: string | null
  userName: string | null
  eventType: AccessLogEventType
  ipAddress: string | null
  timestamp: string
}

/** A page of results plus its total count. */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Ledger Activity filters plus paging, mapped to the GET query string. */
export interface AuditLogQuery {
  page: number
  pageSize: number
  entityType?: string
  action?: string
  performedBy?: string
  dateFrom?: string
  dateTo?: string
}

/** Access Activity filters plus paging, mapped to the GET query string. */
export interface AccessLogQuery {
  page: number
  pageSize: number
  eventType?: string
  userId?: string
  dateFrom?: string
  dateTo?: string
}
