export function formatAvoidFour(value: number | null | undefined): string {
  if (value == null) return '';
  if (value < 0) return String(value);
  const ones = value % 10;
  if (ones === 4) {
    const base = value - 1;
    return `${base}-1`;
  }
  return String(value);
}
