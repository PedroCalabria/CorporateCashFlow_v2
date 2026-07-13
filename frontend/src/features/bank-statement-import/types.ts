export type BankStatementBatchStatus = 'Processed' | 'ProcessedWithErrors' | 'Rejected'

export type BankStatementLineType = 'Credit' | 'Debit'

/** A bank-statement import batch as returned by the API, with a summary of its lines. */
export interface BankStatementBatch {
  id: string
  subsidiaryId: string
  fileName: string
  importedBy: string
  importedAt: string
  status: BankStatementBatchStatus
  lineCount: number
  unmatchedCount: number
  invalidatedCount: number
  rejectedBy: string | null
  rejectedAt: string | null
  rejectionReason: string | null
}

/** A page of results plus its total count. */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** List filters plus paging, mapped to the GET query string. */
export interface BankStatementBatchQuery {
  page: number
  pageSize: number
  subsidiaryId?: string
  status?: string
  dateFrom?: string
  dateTo?: string
}

/** Result of a statement import: the created batch, created line count, and per-row rejection reasons. */
export interface ImportResult {
  batchId: string
  status: BankStatementBatchStatus
  createdCount: number
  errors: { rowNumber: number; message: string }[]
}
