/** Today's date in the browser's time zone, as "YYYY-MM-DD". */
export function todayIso(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

/** The year of a "YYYY-MM-DD" date, or an empty string. */
export function yearOf(date: string | null | undefined): string {
  return date ? date.slice(0, 4) : '';
}
