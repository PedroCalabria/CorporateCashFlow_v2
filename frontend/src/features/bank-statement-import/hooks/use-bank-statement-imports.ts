import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { listSubsidiaries } from '@/features/subsidiaries/api/subsidiaries-api'
import { importBankStatement, listBankStatementBatches, rejectBatch } from '../api/bank-statement-import-api'
import type { BankStatementBatchQuery } from '../types'

/** Base query key; the list key includes the active filters/paging so each page/filter caches separately. */
export const bankStatementBatchesKey = ['bank-statement-imports'] as const

export function useBankStatementBatches(query: BankStatementBatchQuery) {
  return useQuery({
    queryKey: [...bankStatementBatchesKey, query],
    queryFn: () => listBankStatementBatches(query),
  })
}

/** Subsidiary options for the Global Manager's subsidiary picker/filter (gated — scoped users get 403). */
export function useSubsidiaryOptions(enabled: boolean) {
  return useQuery({ queryKey: ['subsidiaries'], queryFn: listSubsidiaries, enabled })
}

function useInvalidateList() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: bankStatementBatchesKey })
}

export function useImportBankStatement() {
  const invalidate = useInvalidateList()
  return useMutation({
    mutationFn: ({ subsidiaryId, file }: { subsidiaryId: string; file: File }) =>
      importBankStatement(subsidiaryId, file),
    onSuccess: invalidate,
  })
}

export function useRejectBatch() {
  const invalidate = useInvalidateList()
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => rejectBatch(id, reason),
    onSuccess: invalidate,
  })
}
