/**
 * Today's local date as an ISO `yyyy-MM-dd` string. Matches the value format of a native
 * `<input type="date">`, and since ISO dates sort lexicographically, a plain string comparison
 * (`value <= todayIso()`) is a safe "not in the future" check with no timezone parsing.
 */
export function todayIso(): string {
  const now = new Date()
  const year = now.getFullYear()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}
