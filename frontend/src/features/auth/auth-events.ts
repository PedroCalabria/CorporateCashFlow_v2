/**
 * Tiny event channel bridging the non-React axios interceptor to the React auth state.
 * When a silent refresh fails, the interceptor emits a forced-logout so the AuthProvider
 * can drop the user and the route guard can redirect to the login screen.
 */
type Listener = () => void

const listeners = new Set<Listener>()

export function onForcedLogout(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function emitForcedLogout(): void {
  for (const listener of listeners) listener()
}
