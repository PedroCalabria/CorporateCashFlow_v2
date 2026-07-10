import { describe, expect, it } from 'vitest'
import { resolveInitialTheme } from './theme'

describe('resolveInitialTheme', () => {
  it('resolves to dark when the OS prefers dark and nothing is saved', () => {
    expect(resolveInitialTheme(null, true)).toBe('dark')
  })

  it('resolves to light when the OS prefers light and nothing is saved', () => {
    expect(resolveInitialTheme(null, false)).toBe('light')
  })

  it('lets a saved preference override the OS preference', () => {
    expect(resolveInitialTheme('light', true)).toBe('light')
    expect(resolveInitialTheme('dark', false)).toBe('dark')
  })

  it('ignores an invalid saved preference and uses the OS preference', () => {
    expect(resolveInitialTheme('nonsense', true)).toBe('dark')
    expect(resolveInitialTheme('', false)).toBe('light')
  })
})
