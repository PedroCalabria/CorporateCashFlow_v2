import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createSubsidiary,
  deactivateSubsidiary,
  listSubsidiaries,
  reactivateSubsidiary,
  updateSubsidiary,
} from '../api/subsidiaries-api'
import type { CreateSubsidiaryInput, UpdateSubsidiaryInput } from '../types'

/** Shared query key for the subsidiaries list; mutations invalidate it to refetch. */
export const subsidiariesQueryKey = ['subsidiaries'] as const

export function useSubsidiaries() {
  return useQuery({
    queryKey: subsidiariesQueryKey,
    queryFn: listSubsidiaries,
  })
}

export function useCreateSubsidiary() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateSubsidiaryInput) => createSubsidiary(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: subsidiariesQueryKey }),
  })
}

export function useUpdateSubsidiary() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateSubsidiaryInput }) =>
      updateSubsidiary(id, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: subsidiariesQueryKey }),
  })
}

export function useDeactivateSubsidiary() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deactivateSubsidiary(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: subsidiariesQueryKey }),
  })
}

export function useReactivateSubsidiary() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => reactivateSubsidiary(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: subsidiariesQueryKey }),
  })
}
