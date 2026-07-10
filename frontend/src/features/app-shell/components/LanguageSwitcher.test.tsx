import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { I18nextProvider } from 'react-i18next'
import i18n from '@/i18n/config'
import { LANGUAGE_STORAGE_KEY } from '@/lib/language'
import { LanguageSwitcher } from './LanguageSwitcher'

describe('LanguageSwitcher toggle behavior', () => {
  it('switches language, updates i18n state, and persists to localStorage', async () => {
    await i18n.changeLanguage('en')
    localStorage.clear()

    render(
      <I18nextProvider i18n={i18n}>
        <LanguageSwitcher />
      </I18nextProvider>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'PT' }))

    await waitFor(() => expect(i18n.language).toBe('pt-BR'))
    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('pt-BR')

    fireEvent.click(screen.getByRole('button', { name: 'EN' }))

    await waitFor(() => expect(i18n.language).toBe('en'))
    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('en')
  })
})
