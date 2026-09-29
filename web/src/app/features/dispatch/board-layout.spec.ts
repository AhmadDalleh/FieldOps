import {
  BOARD_MINUTES, addDays, placeJob, resizedEnd, blockGeometry, boardMinute, isoAtBoardMinute, pixelsToMinutes, snap, workingHoursGeometry,
} from './board-layout';

describe('board layout', () => {
  const date = '2026-10-02';

  it('measures minutes from 07:00 Dubai, which is 03:00 UTC', () => {
    expect(boardMinute('2026-10-02T03:00:00Z', date)).toBe(0);
    expect(boardMinute('2026-10-02T04:30:00Z', date)).toBe(90);
    expect(isoAtBoardMinute(date, 90)).toBe('2026-10-02T04:30:00.000Z');
    expect(isoAtBoardMinute(date, BOARD_MINUTES)).toBe('2026-10-02T16:00:00.000Z');
  });

  it('places a block in percent of the 13-hour day', () => {
    const g = blockGeometry('2026-10-02T04:00:00Z', '2026-10-02T06:00:00Z', date)!;
    expect(g.left).toBeCloseTo((60 / BOARD_MINUTES) * 100);
    expect(g.width).toBeCloseTo((120 / BOARD_MINUTES) * 100);
  });

  it('clips blocks to the board and drops those outside it', () => {
    const g = blockGeometry('2026-10-02T01:00:00Z', '2026-10-02T04:00:00Z', date)!;
    expect(g.left).toBe(0);
    expect(g.width).toBeCloseTo((60 / BOARD_MINUTES) * 100);
    expect(blockGeometry('2026-10-02T17:00:00Z', '2026-10-02T18:00:00Z', date)).toBeNull();
    expect(blockGeometry('2026-10-01T05:00:00Z', '2026-10-01T06:00:00Z', date)).toBeNull();
  });

  it('snaps to 15 minutes', () => {
    expect(snap(7)).toBe(0);
    expect(snap(8)).toBe(15);
    expect(snap(52)).toBe(45);
    expect(snap(-8)).toBe(-15);
  });

  it('converts pixels to minutes', () => {
    expect(pixelsToMinutes(390, 780)).toBe(BOARD_MINUTES / 2);
    expect(pixelsToMinutes(10, 0)).toBe(0);
  });

  it('shades working hours', () => {
    const g = workingHoursGeometry('08:00:00', '17:00:00');
    expect(g.left).toBeCloseTo((60 / BOARD_MINUTES) * 100);
    expect(g.width).toBeCloseTo((540 / BOARD_MINUTES) * 100);
  });

  it('moves between days', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-10-01', -1)).toBe('2026-09-30');
  });

  it('places a dropped job on the grid with the default two hours', () => {
    expect(placeJob(97)).toEqual({ start: 90, end: 210 });
    expect(placeJob(-30)).toEqual({ start: 0, end: 120 });
    expect(placeJob(BOARD_MINUTES + 50)).toEqual({ start: BOARD_MINUTES - 15, end: BOARD_MINUTES + 105 });
  });

  it('resizes to at least 15 minutes and at most 12 hours', () => {
    expect(resizedEnd(60, 180, 37)).toBe(210);
    expect(resizedEnd(60, 180, -500)).toBe(75);
    expect(resizedEnd(0, 600, 400)).toBe(720);
  });
});
