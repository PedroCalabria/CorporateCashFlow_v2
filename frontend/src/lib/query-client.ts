import { QueryClient } from '@tanstack/react-query'

/**
 * Single TanStack Query client for the app (docs/technical-architecture.md §3.2 — server state
 * lives in TanStack Query). Introduced with the first data-fetching capability (subsidiaries);
 * later features reuse this same client. Window-focus refetch is off to keep the dev experience
 * quiet; one retry smooths over transient blips without masking real failures.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})
