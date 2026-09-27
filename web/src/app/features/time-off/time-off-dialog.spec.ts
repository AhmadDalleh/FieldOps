import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { TimeOffDialog } from './time-off-dialog';

describe('TimeOffDialog', () => {
  let backend: HttpTestingController;
  const close = vi.fn();

  function setup(data: unknown) {
    close.mockReset();
    TestBed.configureTestingModule({
      imports: [TimeOffDialog],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  }

  afterEach(() => backend.verify());

  async function fill(startsAt: string, endsAt: string) {
    const fixture = TestBed.createComponent(TimeOffDialog);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const type = (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    type('input[formcontrolname=startsAt]', startsAt);
    type('input[formcontrolname=endsAt]', endsAt);
    type('textarea[formcontrolname=reason]', ' Family event ');
    await fixture.whenStable();
    return el;
  }

  it('sends the Dubai times as UTC for a technician', async () => {
    setup({});
    const el = await fill('2026-10-01T08:00', '2026-10-01T17:00');

    expect(el.querySelector('mat-select')).toBeNull();
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();

    const req = backend.expectOne('/api/time-off');
    expect(req.request.body).toEqual({
      startsAt: '2026-10-01T04:00:00.000Z',
      endsAt: '2026-10-01T13:00:00.000Z',
      reason: 'Family event',
      technicianId: null,
    });
    req.flush({ id: 't1' });
    expect(close).toHaveBeenCalledWith(true);
  });

  it('blocks an end that is not after the start', async () => {
    setup({});
    const el = await fill('2026-10-01T17:00', '2026-10-01T08:00');

    expect(el.textContent).toContain('The end must be after the start.');
    expect(el.querySelector<HTMLButtonElement>('button[type=submit]')!.disabled).toBe(true);
  });

  it('requires office staff to choose a technician', async () => {
    setup({ technicians: [{ id: 'tech-1', name: 'Tech One' }] });
    const el = await fill('2026-10-01T08:00', '2026-10-01T17:00');

    expect(el.querySelector('mat-select')).not.toBeNull();
    expect(el.querySelector<HTMLButtonElement>('button[type=submit]')!.disabled).toBe(true);
  });
});
