import { apiClient } from '@/lib/api-client'
import type {
  CreateUserInput,
  CreateUserResult,
  ResetPasswordInput,
  UpdateUserInput,
  User,
} from '../types'

/**
 * Typed API calls for the user-management capability. Every endpoint is Manager-only on the
 * backend; the fine-grained scope rules (Global vs Subsidiary Manager) are enforced server-side —
 * these calls just surface the resulting `403`/validation errors to the UI.
 */

export async function listUsers(): Promise<User[]> {
  const { data } = await apiClient.get<User[]>('/users')
  return data
}

export async function createUser(input: CreateUserInput): Promise<CreateUserResult> {
  const { data } = await apiClient.post<CreateUserResult>('/users', input)
  return data
}

export async function updateUser(id: string, input: UpdateUserInput): Promise<User> {
  const { data } = await apiClient.put<User>(`/users/${id}`, input)
  return data
}

export async function deactivateUser(id: string): Promise<User> {
  const { data } = await apiClient.patch<User>(`/users/${id}/deactivate`)
  return data
}

export async function reactivateUser(id: string): Promise<User> {
  const { data } = await apiClient.patch<User>(`/users/${id}/reactivate`)
  return data
}

export async function resetPassword(id: string, input: ResetPasswordInput): Promise<void> {
  await apiClient.patch(`/users/${id}/reset-password`, input)
}
