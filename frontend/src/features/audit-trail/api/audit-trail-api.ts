import { apiClient } from '@/lib/api-client'
import type { AccessLogEntry, AccessLogQuery, AuditLogEntry, AuditLogQuery, PagedResult } from '../types'

/**
 * Typed API calls for the audit-trail capability. Both endpoints are scoped by the backend to the
 * caller's role (Manager/Auditor only, own subsidiary or global) — an Editor gets `403`.
 */

export async function listAuditLog(query: AuditLogQuery): Promise<PagedResult<AuditLogEntry>> {
  const params: Record<string, string | number> = { page: query.page, pageSize: query.pageSize }
  if (query.entityType) params.entityType = query.entityType
  if (query.action) params.action = query.action
  if (query.performedBy) params.performedBy = query.performedBy
  if (query.dateFrom) params.dateFrom = query.dateFrom
  if (query.dateTo) params.dateTo = query.dateTo

  const { data } = await apiClient.get<PagedResult<AuditLogEntry>>('/audit-log', { params })
  return data
}

export async function listAccessLog(query: AccessLogQuery): Promise<PagedResult<AccessLogEntry>> {
  const params: Record<string, string | number> = { page: query.page, pageSize: query.pageSize }
  if (query.eventType) params.eventType = query.eventType
  if (query.userId) params.userId = query.userId
  if (query.dateFrom) params.dateFrom = query.dateFrom
  if (query.dateTo) params.dateTo = query.dateTo

  const { data } = await apiClient.get<PagedResult<AccessLogEntry>>('/access-log', { params })
  return data
}
