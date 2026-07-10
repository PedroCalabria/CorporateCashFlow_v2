import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { ThemeProvider } from './ThemeProvider'
import { useTheme } from './use-theme'
import { THEME_STORAGE_KEY } from '@/lib/theme'

function ThemeProbe() {
  const { theme, toggleTheme } = useTheme()
  return (
    <button onClick={toggleTheme}>
      current: {theme}
    </button>
  )
}

describe('ThemeProvider toggle behavior', () => {
  it('toggles theme, updates state, flips the root class, and persists to localStorage', () => {
    render(
      <ThemeProvider>
        <ThemeProbe />
      </ThemeProvider>,
    )

    // Initial: matchMedia mocked to light, nothing saved.
    expect(screen.getByRole('button')).toHaveTextContent('current: light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')

    fireEvent.click(screen.getByRole('button'))

    expect(screen.getByRole('button')).toHaveTextContent('current: dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
  })
})
