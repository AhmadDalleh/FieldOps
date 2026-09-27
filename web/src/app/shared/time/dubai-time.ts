/** Business times are shown and entered in Dubai time, which is UTC+4 all year (no daylight saving). */
const DUBAI_OFFSET_MS = 4 * 60 * 60 * 1000;

/** Converts a `datetime-local` value entered in Dubai time (`2026-10-01T08:00`) to a UTC ISO string. */
export function dubaiLocalToUtc(local: string): string {
  const [date, time] = local.split('T');
  const [y, m, d] = date.split('-').map(Number);
  const [hh, mm] = time.split(':').map(Number);
  return new Date(Date.UTC(y, m - 1, d, hh, mm) - DUBAI_OFFSET_MS).toISOString();
}

/** Converts a UTC instant to a `datetime-local` value in Dubai time. */
export function utcToDubaiLocal(iso: string): string {
  return new Date(new Date(iso).getTime() + DUBAI_OFFSET_MS).toISOString().slice(0, 16);
}

/** Today's date in Dubai as `yyyy-MM-dd`. */
export function dubaiToday(now = new Date()): string {
  return new Date(now.getTime() + DUBAI_OFFSET_MS).toISOString().slice(0, 10);
}

const formatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Dubai',
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

/** Formats a UTC instant in Dubai time, e.g. `1 Oct 2026, 08:00`. */
export function formatDubai(iso: string): string {
  return formatter.format(new Date(iso));
}
