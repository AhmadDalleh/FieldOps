import { move, statusLabel } from './work-order-labels';

describe('work order labels', () => {
  it('spells out multi-word statuses', () => {
    expect(statusLabel('OnHold')).toBe('On hold');
    expect(statusLabel('EnRoute')).toBe('En route');
    expect(statusLabel('InProgress')).toBe('In progress');
    expect(statusLabel('New')).toBe('New');
  });

  it('moves an item up or down and ignores moves past the ends', () => {
    expect(move(['a', 'b', 'c'], 1, -1)).toEqual(['b', 'a', 'c']);
    expect(move(['a', 'b', 'c'], 1, 1)).toEqual(['a', 'c', 'b']);
    expect(move(['a', 'b', 'c'], 0, -1)).toEqual(['a', 'b', 'c']);
    expect(move(['a', 'b', 'c'], 2, 1)).toEqual(['a', 'b', 'c']);
  });
});
