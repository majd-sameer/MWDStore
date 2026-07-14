
export function dayBoundary(date: Date | null, end: boolean): string | undefined {
  if (!date) {
    return undefined;
  }
  const y = date.getFullYear();
  const m = `${date.getMonth() + 1}`.padStart(2, '0');
  const d = `${date.getDate()}`.padStart(2, '0');
  return `${y}-${m}-${d}T${end ? '23:59:59' : '00:00:00'}`;
}
