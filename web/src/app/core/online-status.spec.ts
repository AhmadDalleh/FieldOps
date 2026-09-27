import { vi } from 'vitest';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { onlineStatus } from './online-status';

@Component({ template: '' })
class Host {
  readonly online = onlineStatus();
}

describe('onlineStatus', () => {
  it('follows the browser online and offline events', () => {
    const fixture = TestBed.createComponent(Host);
    const spy = vi.spyOn(navigator, 'onLine', 'get');

    spy.mockReturnValue(false);
    window.dispatchEvent(new Event('offline'));
    expect(fixture.componentInstance.online()).toBe(false);

    spy.mockReturnValue(true);
    window.dispatchEvent(new Event('online'));
    expect(fixture.componentInstance.online()).toBe(true);
    spy.mockRestore();
  });
});
