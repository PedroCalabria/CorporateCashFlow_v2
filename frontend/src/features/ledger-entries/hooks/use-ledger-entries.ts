import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { listSubsidiaries } from '@/features/subsidiaries/api/subsidiaries-api'
import {
  createLedgerEntry,
  deleteLedgerEntry,
  importLedgerEntries,
  listCategories,
  listLedgerEntries,
  updateLedgerEntry,
} from '../api/ledger-entries-api'
import type { CreateLedgerEntryInput, LedgerEntryQuery, UpdateLedgerEntryInput } from '../types'

/** Base query key; the list key includes the active filters/paging so each page/filter caches separately. */
export const ledgerEntriesKey = ['ledger-entries'] as const

export function useLedgerEntries(query: LedgerEntryQuery) {
  return useQuery({
    queryKey: [...ledgerEntriesKey, query],
    queryFn: () => listLedgerEntries(query),
  })
}

export function useCategories() {
  return useQuery({
    queryKey: ['ledger-categories'],
    queryFn: listCategories,
    staleTime: Infinity, // fixed catalog — never changes at runtime
  })
}

/** Subsidiary options for the Global Manager's subsidiary picker/filter (gated — scoped users get 403). */
export function useSubsidiaryOptions(enabled: boolean) {
  return useQuery({ queryKey: ['subsidiaries'], queryFn: listSubsidiaries, enabled })
}

function useInvalidateList() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ledgerEntriesKey })
}

export function useCreateLedgerEntry() {
  const invalidate = useInvalidateList()
  return useMutation({
    mutationFn: (input: CreateLedgerEntryInput) => createLedgerEntry(input),
    onSuccess: invalidate,
  })
}

export function useUpdateLedgerEntry() {
  const invalidate = useInvalidateList()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateLedgerEntryInput }) => updateLedgerEntry(id, input),
    onSuccess: invalidate,
  })
}

export function useDeleteLedgerEntry() {
  const invalidate = useInvalidateList()
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => deleteLedgerEntry(id, reason),
    onSuccess: invalidate,
  })
}

export function useImportLedgerEntries() {
  const invalidate = useInvalidateList()
  return useMutation({
    mutationFn: ({ subsidiaryId, file }: { subsidiaryId: string; file: File }) =>
      importLedgerEntries(subsidiaryId, file),
    onSuccess: invalidate,
  })
}
