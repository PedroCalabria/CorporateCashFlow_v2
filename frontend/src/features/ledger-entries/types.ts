export type LedgerEntryStatus = 'Open' | 'PendingReconciliation' | 'PendingApproval' | 'Reconciled' | 'Deleted'

export type LedgerEntryType = 'Credit' | 'Debit'

export type CategoryType = 'Income' | 'Expense'

/** A catalog category, for the create/edit forms and the category filter. */
export interface Category {
  id: string
  name: string
  code: string
  type: CategoryType
}

/** A ledger entry as returned by the API. */
export interface LedgerEntry {
  id: string
  subsidiaryId: string
  categoryId: string
  type: LedgerEntryType
  amount: number
  date: string
  description: string
  status: LedgerEntryStatus
  createdBy: string
  createdAt: string
  updatedAt: string
  deletionReason: string | null
}

/** A page of results plus its total count. */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface CreateLedgerEntryInput {
  subsidiaryId: string
  categoryId: string
  amount: number
  date: string
  description: string
}

export interface UpdateLedgerEntryInput {
  categoryId: string
  amount: number
  date: string
  description: string
}

/** List filters plus paging, mapped to the GET query string. */
export interface LedgerEntryQuery {
  page: number
  pageSize: number
  subsidiaryId?: string
  categoryId?: string
  dateFrom?: string
  dateTo?: string
  status?: string
}

/** Result of a spreadsheet import: created count and per-row rejection reasons. */
export interface ImportResult {
  createdCount: number
  errors: { rowNumber: number; message: string }[]
}
