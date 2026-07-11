import { cn } from '@/lib/utils'

/** Shared input/select styling, matching the login and subsidiaries forms. */
export const controlClass =
  'flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50'

/** The control's class with an error-aware destructive border. */
export function controlClassWithError(hasError: boolean): string {
  return cn(controlClass, hasError && 'border-destructive')
}
