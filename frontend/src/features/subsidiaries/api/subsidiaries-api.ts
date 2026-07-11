import { apiClient } from '@/lib/api-client'
import type { CreateSubsidiaryInput, Subsidiary, UpdateSubsidiaryInput } from '../types'

/**
 * Typed API calls for the subsidiaries capability. Every endpoint is Global-Manager-only on the
 * backend; a subsidiary-scoped session receives `403` here. The update call deliberately carries
 * only Name/Code — the immutable baseline has no field on the client contract either.
 */

export async function listSubsidiaries(): Promise<Subsidiary[]> {
  const { data } = await apiClient.get<Subsidiary[]>('/subsidiaries')
  return data
}

export async function createSubsidiary(input: CreateSubsidiaryInput): Promise<Subsidiary> {
  const { data } = await apiClient.post<Subsidiary>('/subsidiaries', input)
  return data
}

export async function updateSubsidiary(id: string, input: UpdateSubsidiaryInput): Promise<Subsidiary> {
  const { data } = await apiClient.put<Subsidiary>(`/subsidiaries/${id}`, input)
  return data
}

export async function deactivateSubsidiary(id: string): Promise<Subsidiary> {
  const { data } = await apiClient.patch<Subsidiary>(`/subsidiaries/${id}/deactivate`)
  return data
}

export async function reactivateSubsidiary(id: string): Promise<Subsidiary> {
  const { data } = await apiClient.patch<Subsidiary>(`/subsidiaries/${id}/reactivate`)
  return data
}
