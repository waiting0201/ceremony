export function currentTaiwanYear(now: Date = new Date()): number {
  return now.getFullYear() - 1911;
}
