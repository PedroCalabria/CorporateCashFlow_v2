import { z } from 'zod'
import type { Role } from './types'

/** Selectable roles. Order matters for the form dropdown. */
export const ALL_ROLES: readonly Role[] = ['Manager', 'Editor', 'Auditor']

/** A Subsidiary Manager may only assign these; a Global Manager may assign any of ALL_ROLES. */
export const SUBSIDIARY_MANAGER_ROLES: readonly Role[] = ['Editor', 'Auditor']

const roleField = z.string().refine((r) => (ALL_ROLES as string[]).includes(r), 'validation.roleRequired')

// subsidiaryId is a string in the form: '' means global scope (null on the wire).
const editorNeedsSubsidiary = (
  val: { role: string; subsidiaryId: string },
  ctx: z.RefinementCtx,
) => {
  if (val.role === 'Editor' && val.subsidiaryId === '') {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['subsidiaryId'],
      message: 'validation.editorNeedsSubsidiary',
    })
  }
}

export const createUserSchema = z
  .object({
    name: z.string().min(1, 'validation.nameRequired'),
    email: z.string().min(1, 'validation.emailRequired').email('validation.emailInvalid'),
    role: roleField,
    subsidiaryId: z.string(),
  })
  .superRefine(editorNeedsSubsidiary)

export const updateUserSchema = z
  .object({
    role: roleField,
    subsidiaryId: z.string(),
  })
  .superRefine(editorNeedsSubsidiary)

export const resetPasswordSchema = z.object({
  newPassword: z
    .string()
    .min(8, 'validation.passwordTooShort')
    .regex(/[A-Za-z]/, 'validation.passwordNeedsLetter')
    .regex(/[0-9]/, 'validation.passwordNeedsDigit'),
})

export type CreateUserFormValues = z.infer<typeof createUserSchema>
export type UpdateUserFormValues = z.infer<typeof updateUserSchema>
export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>
