import { NumericFormat } from 'react-number-format'

/**
 * Currency-masked amount input (BRL): displays `R$ 1.234,56` (pt-BR grouping) while emitting a plain
 * `number` upward, so a Zod `z.coerce.number()` field keeps working unchanged. Meant to be driven by
 * a React Hook Form `Controller` (value/onChange/onBlur are the field's). The caller supplies the
 * input `className` so it matches the surrounding form's styling, and `allowNegative` (default
 * `false`) enables negative values where the domain permits them (e.g. an initial bank balance).
 */
export function CurrencyInput({
  id,
  name,
  value,
  onChange,
  onBlur,
  className,
  allowNegative = false,
}: {
  id: string
  name?: string
  value: number | undefined
  onChange: (value: number | undefined) => void
  onBlur?: () => void
  className?: string
  allowNegative?: boolean
}) {
  return (
    <NumericFormat
      id={id}
      name={name}
      value={value ?? ''}
      onBlur={onBlur}
      onValueChange={(values) => onChange(values.floatValue)}
      thousandSeparator="."
      decimalSeparator=","
      decimalScale={2}
      fixedDecimalScale
      allowNegative={allowNegative}
      prefix="R$ "
      inputMode="decimal"
      placeholder="R$ 0,00"
      className={className}
    />
  )
}
