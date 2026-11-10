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

/** Parses a "YYYY-MM-DD" date as a local date (avoids the UTC shift of `new Date(iso)`). */
function parseIso(date: string): Date {
  const [year, month, day] = date.split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** The month of a "YYYY-MM-DD" date, e.g. "November 2026". */
export function formatMonth(date: string): string {
  return parseIso(date).toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
}

/** The day within its month, e.g. "Sat 7". */
export function formatDay(date: string): string {
  const parsed = parseIso(date);
  const weekday = parsed.toLocaleDateString('en-US', { weekday: 'short' });
  return `${weekday} ${parsed.getDate()}`;
}

/** A full, human-friendly date, e.g. "November 7, 2026". */
export function formatDate(date: string): string {
  return parseIso(date).toLocaleDateString('en-US', {
    month: 'long',
    day: 'numeric',
    year: 'numeric',
  });
}

/** A point in time in the browser's time zone, e.g. "Nov 7, 2026, 9:30 PM". */
export function formatDateTime(timestamp: string): string {
  return new Date(timestamp).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}
