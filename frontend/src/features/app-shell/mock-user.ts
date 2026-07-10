/**
 * Mocked current user. Authentication does not exist yet (see proposal.md
 * "Out of Scope"); the `auth` capability will replace this with the real
 * signed-in user. Kept as a plain constant so the shell has something to show
 * in the menu header.
 */
export interface CurrentUser {
  name: string
  initials: string
}

export const mockCurrentUser: CurrentUser = {
  name: 'Ada Lovelace',
  initials: 'AL',
}
