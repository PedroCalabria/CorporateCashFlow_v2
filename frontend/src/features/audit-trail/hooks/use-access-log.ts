import { useQuery } from '@tanstack/react-query'
import { listAccessLog } from '../api/audit-trail-api'
import type { AccessLogQuery } from '../types'

/** Query key includes the active filters/paging so each page/filter combination caches separately. */
export function useAccessLog(query: AccessLogQuery) {
  return useQuery({
    queryKey: ['access-log', query],
    queryFn: () => listAccessLog(query),
  })
}
