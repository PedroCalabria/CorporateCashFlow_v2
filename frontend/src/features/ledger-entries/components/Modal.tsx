import type { ReactNode } from 'react'

/** Minimal accessible modal used by the ledger-entries dialogs (feature-local, like the other features). */
export function Modal({
  title,
  onClose,
  children,
}: {
  title: string
  onClose: () => void
  children: ReactNode
}) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-lg border border-border bg-card p-6 shadow-lg"
        onClick={(event) => event.stopPropagation()}
      >
        <h2 className="text-lg font-semibold tracking-tight">{title}</h2>
        <div className="mt-4">{children}</div>
      </div>
    </div>
  )
}
