import { z } from 'zod'
import { todayIso } from '@/lib/date'

/**
 * Form schemas for the subsidiaries screens. Messages are i18n keys (resolved in the
 * `subsidiaries` namespace at render time). The create schema mirrors the backend validator
 * (Name/Code required, a numeric InitialBalance, a required ReferenceDate); the update schema
 * carries Name/Code only — the immutable baseline is not part of any edit contract (design.md §D4).
 */
export const createSubsidiarySchema = z.object({
  name: z.string().min(1, 'validation.nameRequired'),
  code: z.string().min(1, 'validation.codeRequired'),
  initialBalance: z.coerce.number({ invalid_type_error: 'validation.balanceInvalid' }),
  referenceDate: z
    .string()
    .min(1, 'validation.referenceDateRequired')
    .refine((value) => value <= todayIso(), 'validation.referenceDateFuture'),
})

export const updateSubsidiarySchema = z.object({
  name: z.string().min(1, 'validation.nameRequired'),
  code: z.string().min(1, 'validation.codeRequired'),
})

export type CreateSubsidiaryFormValues = z.infer<typeof createSubsidiarySchema>
export type UpdateSubsidiaryFormValues = z.infer<typeof updateSubsidiarySchema>
