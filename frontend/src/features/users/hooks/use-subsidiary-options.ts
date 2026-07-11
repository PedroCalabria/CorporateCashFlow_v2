import { useQuery } from '@tanstack/react-query'
import { listSubsidiaries } from '@/features/subsidiaries/api/subsidiaries-api'

/**
 * Subsidiary options for the user forms/list. Only a Global Manager may call `GET /api/subsidiaries`
 * (a Subsidiary Manager gets 403), so the query is gated by <paramref name="enabled"/>. Shares the
 * `['subsidiaries']` cache key with the subsidiaries feature, so the data is reused when present.
 */
export function useSubsidiaryOptions(enabled: boolean) {
  return useQuery({
    queryKey: ['subsidiaries'],
    queryFn: listSubsidiaries,
    enabled,
  })
}
