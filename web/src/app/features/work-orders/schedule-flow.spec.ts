import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { addHoursLocal, nextFullHour } from './schedule-dialog';
import { ScheduleFlow } from './schedule-flow';

describe('ScheduleFlow', () => {
  let backend: HttpTestingController;
  let confirm: boolean;
  const openDialog = vi.fn(() => ({ afterClosed: () => of(confirm) }));
  const openSnack = vi.fn();
  const slot = { technicianId: 't-1', start: '2026-10-02T04:00:00Z', end: '2026-10-02T06:00:00Z' };
  const overlap = { code: 'Schedule.Overlap', title: 'The technician already has WO-000001 at this time. Schedule anyway?' };

  beforeEach(() => {
    openDialog.mockClear();
    openSnack.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MatDialog, useValue: { open: openDialog } },
        { provide: MatSnackBar, useValue: { open: openSnack } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('asks about an overlap and retries with allowOverlap, then shows the warnings', () => {
    confirm = true;
    const next = vi.fn();
    TestBed.inject(ScheduleFlow).schedule('wo-2', slot).subscribe(next);

    backend.expectOne('/api/work-orders/wo-2/schedule').flush(overlap, { status: 409, statusText: 'Conflict' });
    expect(openDialog).toHaveBeenCalledOnce();

    const retry = backend.expectOne('/api/work-orders/wo-2/schedule');
    expect(retry.request.body).toEqual({ ...slot, allowOverlap: true });
    retry.flush({ workOrder: { id: 'wo-2' }, warnings: ['Overlaps WO-000001'] });

    expect(next).toHaveBeenCalledWith({ workOrder: { id: 'wo-2' }, warnings: ['Overlaps WO-000001'] });
    expect(openSnack).toHaveBeenCalledWith('Overlaps WO-000001', 'Close', expect.anything());
  });

  it('saves nothing when the dispatcher declines the overlap', () => {
    confirm = false;
    const complete = vi.fn();
    TestBed.inject(ScheduleFlow).schedule('wo-2', slot).subscribe({ complete });

    backend.expectOne('/api/work-orders/wo-2/schedule').flush(overlap, { status: 409, statusText: 'Conflict' });

    expect(complete).toHaveBeenCalled();
    backend.expectNone('/api/work-orders/wo-2/schedule');
  });

  it('passes other errors through without asking', () => {
    const error = vi.fn();
    TestBed.inject(ScheduleFlow).schedule('wo-2', slot).subscribe({ error });

    backend
      .expectOne('/api/work-orders/wo-2/schedule')
      .flush({ code: 'Technician.OnTimeOff', title: 'On time off' }, { status: 409, statusText: 'Conflict' });

    expect(openDialog).not.toHaveBeenCalled();
    expect(error).toHaveBeenCalled();
  });
});

describe('schedule dialog times', () => {
  it('defaults to the next full hour in Dubai time', () => {
    expect(nextFullHour(new Date('2026-10-02T04:20:00Z'))).toBe('2026-10-02T09:00');
    expect(nextFullHour(new Date('2026-10-02T04:00:00Z'))).toBe('2026-10-02T08:00');
  });

  it('adds hours across midnight', () => {
    expect(addHoursLocal('2026-10-02T23:00', 2)).toBe('2026-10-03T01:00');
  });
});
