import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  approveEntry,
  getReconciliationBoard,
  justifyEntry,
  manualMatch,
  rejectEntry,
} from '../api/reconciliation-api'

/** Base query key for the reconciliation board. */
export const reconciliationBoardKey = ['reconciliation', 'board'] as const

export function useReconciliationBoard() {
  return useQuery({
    queryKey: reconciliationBoardKey,
    queryFn: getReconciliationBoard,
  })
}

/** Any reconciliation write can change entries and lines, so invalidate the board and the ledger-entries list. */
function useInvalidateAfterWrite() {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: reconciliationBoardKey })
    void queryClient.invalidateQueries({ queryKey: ['ledger-entries'] })
  }
}

export function useManualMatch() {
  const invalidate = useInvalidateAfterWrite()
  return useMutation({
    mutationFn: ({ ledgerEntryId, bankStatementLineId }: { ledgerEntryId: string; bankStatementLineId: string }) =>
      manualMatch(ledgerEntryId, bankStatementLineId),
    onSuccess: invalidate,
  })
}

export function useJustifyEntry() {
  const invalidate = useInvalidateAfterWrite()
  return useMutation({
    mutationFn: ({ id, justificationText }: { id: string; justificationText: string }) =>
      justifyEntry(id, justificationText),
    onSuccess: invalidate,
  })
}

export function useApproveEntry() {
  const invalidate = useInvalidateAfterWrite()
  return useMutation({
    mutationFn: (id: string) => approveEntry(id),
    onSuccess: invalidate,
  })
}

export function useRejectEntry() {
  const invalidate = useInvalidateAfterWrite()
  return useMutation({
    mutationFn: ({ id, rejectionReason }: { id: string; rejectionReason: string }) => rejectEntry(id, rejectionReason),
    onSuccess: invalidate,
  })
}
