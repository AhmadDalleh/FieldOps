import { expiresSoon } from './realtime';

function jwt(payload: object): string {
  const encode = (o: object) => btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${encode({ alg: 'HS256' })}.${encode(payload)}.signature`;
}

describe('expiresSoon', () => {
  const now = Date.UTC(2026, 9, 1, 6, 0, 0);

  it('keeps a token with more than the margin left', () => {
    expect(expiresSoon(jwt({ exp: now / 1000 + 600 }), now)).toBe(false);
  });

  it('refreshes a token about to expire or already expired', () => {
    expect(expiresSoon(jwt({ exp: now / 1000 + 10 }), now)).toBe(true);
    expect(expiresSoon(jwt({ exp: now / 1000 - 60 }), now)).toBe(true);
  });

  it('refreshes a token it cannot read', () => {
    expect(expiresSoon('not-a-jwt', now)).toBe(true);
    expect(expiresSoon(jwt({ sub: 'x' }), now)).toBe(true);
  });
});
