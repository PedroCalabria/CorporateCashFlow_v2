export type LedgerEntryType = 'Credit' | 'Debit'

/** A ledger entry available for matching or justification (Open or PendingReconciliation). */
export interface PendingLedgerEntry {
  id: string
  subsidiaryId: string
  type: LedgerEntryType
  amount: number
  date: string
  description: string
  status: 'Open' | 'PendingReconciliation'
  justificationText: string | null
  rejectionReason: string | null
}

/** An unmatched bank statement line available to be matched against a pending entry. */
export interface UnmatchedLine {
  id: string
  subsidiaryId: string
  date: string
  amount: number
  type: LedgerEntryType
  description: string
  documentNumber: string | null
}

/** A ledger entry awaiting a Manager decision (PendingApproval) — the approve/reject queue. */
export interface PendingApprovalEntry {
  id: string
  subsidiaryId: string
  type: LedgerEntryType
  amount: number
  date: string
  description: string
  justificationText: string | null
}

/** The reconciliation board scoped to the caller (entries, unmatched lines, and the approval queue). */
export interface ReconciliationBoard {
  pendingEntries: PendingLedgerEntry[]
  unmatchedLines: UnmatchedLine[]
  pendingApprovals: PendingApprovalEntry[]
}
