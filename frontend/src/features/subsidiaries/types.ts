/** A subsidiary with its bank-account baseline, as returned by the subsidiaries endpoints. */
export interface Subsidiary {
  id: string
  name: string
  code: string
  isActive: boolean
  initialBalance: number
  /** ISO date (yyyy-mm-dd). Immutable baseline — display only. */
  referenceDate: string
}

/** Body for creating a subsidiary (and its bank account) — the only place the baseline is set. */
export interface CreateSubsidiaryInput {
  name: string
  code: string
  initialBalance: number
  referenceDate: string
}

/** Body for editing a subsidiary — Name/Code only; the baseline is never editable. */
export interface UpdateSubsidiaryInput {
  name: string
  code: string
}
