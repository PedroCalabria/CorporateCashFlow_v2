import { apiClient } from '@/lib/api-client'
import type {
  Category,
  CreateLedgerEntryInput,
  ImportResult,
  LedgerEntry,
  LedgerEntryQuery,
  PagedResult,
  UpdateLedgerEntryInput,
} from '../types'

/**
 * Typed API calls for the ledger-entries capability. Reads are scoped by the backend to the
 * caller's role; write calls surface the backend's `403`/`409`/validation errors to the UI.
 */

export async function listLedgerEntries(query: LedgerEntryQuery): Promise<PagedResult<LedgerEntry>> {
  const params: Record<string, string | number> = { page: query.page, pageSize: query.pageSize }
  if (query.subsidiaryId) params.subsidiaryId = query.subsidiaryId
  if (query.categoryId) params.categoryId = query.categoryId
  if (query.dateFrom) params.dateFrom = query.dateFrom
  if (query.dateTo) params.dateTo = query.dateTo
  if (query.status) params.status = query.status

  const { data } = await apiClient.get<PagedResult<LedgerEntry>>('/ledger-entries', { params })
  return data
}

export async function listCategories(): Promise<Category[]> {
  const { data } = await apiClient.get<Category[]>('/ledger-entries/categories')
  return data
}

export async function createLedgerEntry(input: CreateLedgerEntryInput): Promise<LedgerEntry> {
  const { data } = await apiClient.post<LedgerEntry>('/ledger-entries', input)
  return data
}

export async function updateLedgerEntry(id: string, input: UpdateLedgerEntryInput): Promise<LedgerEntry> {
  const { data } = await apiClient.put<LedgerEntry>(`/ledger-entries/${id}`, input)
  return data
}

export async function deleteLedgerEntry(id: string, deletionReason: string): Promise<void> {
  // A DELETE with a body carries the mandatory reason.
  await apiClient.delete(`/ledger-entries/${id}`, { data: { deletionReason } })
}

export async function importLedgerEntries(subsidiaryId: string, file: File): Promise<ImportResult> {
  const form = new FormData()
  form.append('subsidiaryId', subsidiaryId)
  form.append('file', file)
  const { data } = await apiClient.post<ImportResult>('/ledger-entries/import', form)
  return data
}
