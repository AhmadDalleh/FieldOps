import { describe, expect, it } from 'vitest';
import { dubaiLocalToUtc, dubaiToday, formatDubai, utcToDubaiLocal } from './dubai-time';

describe('dubai time', () => {
  it('converts a Dubai local time to UTC', () => {
    expect(dubaiLocalToUtc('2026-10-01T08:00')).toBe('2026-10-01T04:00:00.000Z');
    expect(dubaiLocalToUtc('2026-10-01T02:30')).toBe('2026-09-30T22:30:00.000Z');
  });

  it('round-trips back to the Dubai local value', () => {
    expect(utcToDubaiLocal('2026-09-30T22:30:00Z')).toBe('2026-10-01T02:30');
  });

  it('reports the Dubai date, which starts four hours before UTC midnight', () => {
    expect(dubaiToday(new Date('2026-09-30T20:00:00Z'))).toBe('2026-10-01');
    expect(dubaiToday(new Date('2026-09-30T19:59:00Z'))).toBe('2026-09-30');
  });

  it('formats instants in Dubai time', () => {
    expect(formatDubai('2026-10-01T04:00:00Z')).toContain('08:00');
    expect(formatDubai('2026-10-01T04:00:00Z')).toContain('Oct');
  });
});
