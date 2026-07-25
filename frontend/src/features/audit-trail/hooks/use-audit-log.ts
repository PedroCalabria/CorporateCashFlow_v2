import { useQuery } from '@tanstack/react-query'
import { listAuditLog } from '../api/audit-trail-api'
import type { AuditLogQuery } from '../types'

/** Query key includes the active filters/paging so each page/filter combination caches separately. */
export function useAuditLog(query: AuditLogQuery) {
  return useQuery({
    queryKey: ['audit-log', query],
    queryFn: () => listAuditLog(query),
  })
}
