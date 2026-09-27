import { HEX_COLOR, toApiTime } from './technician-dialog';

describe('technician dialog helpers', () => {
  it('accepts only six-digit hex colors', () => {
    expect(HEX_COLOR.test('#1E88E5')).toBe(true);
    expect(HEX_COLOR.test('#abcdef')).toBe(true);
    expect(HEX_COLOR.test('1E88E5')).toBe(false);
    expect(HEX_COLOR.test('#1E88E')).toBe(false);
    expect(HEX_COLOR.test('blue')).toBe(false);
  });

  it('adds seconds to a time input value', () => {
    expect(toApiTime('08:00')).toBe('08:00:00');
    expect(toApiTime('17:30:00')).toBe('17:30:00');
  });
});
