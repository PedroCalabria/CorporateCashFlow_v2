import { describe, expect, it } from 'vitest'
import { resolveInitialLanguage } from './language'

describe('resolveInitialLanguage', () => {
  it('resolves a pt-BR navigator locale to pt-BR', () => {
    expect(resolveInitialLanguage(null, ['pt-BR'])).toBe('pt-BR')
  })

  it('prefix-matches a bare pt locale to pt-BR', () => {
    expect(resolveInitialLanguage(null, ['pt'])).toBe('pt-BR')
    expect(resolveInitialLanguage(null, ['pt-PT'])).toBe('pt-BR')
  })

  it('falls back to English for English or unsupported locales', () => {
    expect(resolveInitialLanguage(null, ['en-US'])).toBe('en')
    expect(resolveInitialLanguage(null, ['fr-FR'])).toBe('en')
    expect(resolveInitialLanguage(null, [])).toBe('en')
    expect(resolveInitialLanguage(null, null)).toBe('en')
  })

  it('lets a saved preference override the navigator locale', () => {
    expect(resolveInitialLanguage('en', ['pt-BR'])).toBe('en')
    expect(resolveInitialLanguage('pt-BR', ['en-US'])).toBe('pt-BR')
  })

  it('ignores an invalid saved preference and detects instead', () => {
    expect(resolveInitialLanguage('de', ['pt-BR'])).toBe('pt-BR')
    expect(resolveInitialLanguage('de', ['en-US'])).toBe('en')
  })
})
