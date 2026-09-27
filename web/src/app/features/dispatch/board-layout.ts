/** Geometry of the dispatch board: one Dubai day from 07:00 to 20:00 (US-DSP-02). Dubai is UTC+4 all year. */
export const BOARD_START_HOUR = 7;
export const BOARD_END_HOUR = 20;
export const BOARD_MINUTES = (BOARD_END_HOUR - BOARD_START_HOUR) * 60;
export const SNAP_MINUTES = 15;
export const DEFAULT_JOB_MINUTES = 120;

const DUBAI_OFFSET_MS = 4 * 60 * 60 * 1000;
const MINUTE_MS = 60 * 1000;

/** UTC milliseconds of 07:00 in Dubai on `date` (yyyy-MM-dd). */
function boardStartMs(date: string): number {
  const [y, m, d] = date.split('-').map(Number);
  return Date.UTC(y, m - 1, d, BOARD_START_HOUR) - DUBAI_OFFSET_MS;
}

/** Minutes from the board's 07:00 to the instant; negative before, above BOARD_MINUTES after. */
export function boardMinute(iso: string, date: string): number {
  return (new Date(iso).getTime() - boardStartMs(date)) / MINUTE_MS;
}

/** The UTC instant at `minutes` after 07:00 Dubai on `date`. */
export function isoAtBoardMinute(date: string, minutes: number): string {
  return new Date(boardStartMs(date) + minutes * MINUTE_MS).toISOString();
}

export function snap(minutes: number, step = SNAP_MINUTES): number {
  return Math.round(minutes / step) * step;
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

/** Left offset and width in percent of the board for a span, clipped to 07:00–20:00; null when outside it. */
export function blockGeometry(startIso: string, endIso: string, date: string): { left: number; width: number } | null {
  const start = clamp(boardMinute(startIso, date), 0, BOARD_MINUTES);
  const end = clamp(boardMinute(endIso, date), 0, BOARD_MINUTES);
  if (end <= start) return null;
  return { left: (start / BOARD_MINUTES) * 100, width: ((end - start) / BOARD_MINUTES) * 100 };
}

/** Converts a horizontal pixel distance on a row of `rowWidth` pixels into board minutes. */
export function pixelsToMinutes(pixels: number, rowWidth: number): number {
  return rowWidth > 0 ? (pixels / rowWidth) * BOARD_MINUTES : 0;
}

/** Shades a technician's working hours (`HH:mm:ss`) on the board, in percent. */
export function workingHoursGeometry(start: string, end: string): { left: number; width: number } {
  const toMinutes = (t: string) => {
    const [h, m] = t.split(':').map(Number);
    return clamp(h * 60 + m - BOARD_START_HOUR * 60, 0, BOARD_MINUTES);
  };
  const s = toMinutes(start);
  const e = toMinutes(end);
  return { left: (s / BOARD_MINUTES) * 100, width: (Math.max(0, e - s) / BOARD_MINUTES) * 100 };
}

export function addDays(date: string, days: number): string {
  const [y, m, d] = date.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + days)).toISOString().slice(0, 10);
}

export const BOARD_HOURS = Array.from({ length: BOARD_END_HOUR - BOARD_START_HOUR }, (_, i) => BOARD_START_HOUR + i);

/** Where a job dropped at `minute` lands: snapped, kept on the board, lasting `duration` minutes. */
export function placeJob(minute: number, duration = DEFAULT_JOB_MINUTES): { start: number; end: number } {
  const start = clamp(snap(minute), 0, BOARD_MINUTES - SNAP_MINUTES);
  return { start, end: start + duration };
}

/** The new end after dragging a block's right edge by `deltaMinutes`: snapped, 15 minutes to 12 hours long. */
export function resizedEnd(start: number, end: number, deltaMinutes: number): number {
  return clamp(snap(end + deltaMinutes), start + SNAP_MINUTES, start + 12 * 60);
}

/** Priority colours shared by the board and the map legend. */
export const PRIORITY_COLORS: Record<string, string> = {
  Urgent: '#c62828',
  High: '#ef6c00',
  Medium: '#1565c0',
  Low: '#78909c',
};

/** Status colours for map pins. */
export const STATUS_COLORS: Record<string, string> = {
  New: '#9e9e9e',
  Scheduled: '#1e88e5',
  Dispatched: '#3949ab',
  EnRoute: '#8e24aa',
  InProgress: '#fb8c00',
  OnHold: '#fdd835',
  Completed: '#43a047',
  Invoiced: '#2e7d32',
  Cancelled: '#e53935',
};
