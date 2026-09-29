import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { dubaiToday } from '../../shared/time/dubai-time';
import { Reports } from './reports';
import { formatMinutes, monthStart, periodProblem } from './reports.api';

describe('report helpers', () => {
  it('formats durations', () => {
    expect(formatMinutes(null)).toBe('–');
    expect(formatMinutes(45)).toBe('45 min');
    expect(formatMinutes(65)).toBe('1 h 05 min');
  });

  it('starts the default period on the first of the month', () => {
    expect(monthStart('2026-10-15')).toBe('2026-10-01');
  });

  it('checks the period like the API does', () => {
    expect(periodProblem('2026-10-01', '2026-10-01')).toBeNull();
    expect(periodProblem('2026-10-02', '2026-10-01')).toMatch(/on or before/);
    expect(periodProblem('2026-01-01', '2027-01-01')).toBeNull(); // 366 days
    expect(periodProblem('2026-01-01', '2027-01-02')).toMatch(/at most 366/);
    expect(periodProblem('', '2026-10-01')).toBe('Choose both dates.');
  });
});

describe('Reports', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Reports],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('loads the month so far, then only the report on screen, with its grouping', async () => {
    const fixture = TestBed.createComponent(Reports);
    TestBed.tick();
    fixture.detectChanges();
    const today = dubaiToday();
    const first = backend.expectOne((r) => r.url === '/api/reports/technicians');
    expect(first.request.params.get('from')).toBe(monthStart(today));
    expect(first.request.params.get('to')).toBe(today);
    first.flush({
      from: '', to: '', completedJobs: 2, averageMinutes: 65, workHours: 2,
      rows: [{ technicianId: 't1', name: 'Sara Ali', employeeCode: 'T-01', completedJobs: 2, averageMinutes: 65, workHours: 2 }],
    });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sara Ali');
    expect(fixture.nativeElement.textContent).toContain('1 h 05 min');

    const buttons = [...fixture.nativeElement.querySelectorAll('mat-button-toggle button')] as HTMLButtonElement[];
    buttons.find((b) => b.textContent?.includes('Revenue'))!.click();
    TestBed.tick();
    fixture.detectChanges();
    const revenue = backend.expectOne((r) => r.url === '/api/reports/revenue');
    expect(revenue.request.params.get('groupBy')).toBe('Month');
    revenue.flush({ from: '', to: '', groupBy: 'Month', rows: [], totals: { key: 'total', label: 'Total', invoices: 0, subtotal: 0, vat: 0, total: 0, paid: 0, outstanding: 0 } });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No invoices were issued in this period.');
    backend.expectNone('/api/reports/technicians');
  });

  it('does not ask the API for a backwards period', async () => {
    const fixture = TestBed.createComponent(Reports);
    TestBed.tick();
    backend.expectOne((r) => r.url === '/api/reports/technicians').flush({ from: '', to: '', rows: [], completedJobs: 0, averageMinutes: null, workHours: 0 });
    const [from] = fixture.nativeElement.querySelectorAll('input[type=date]') as HTMLInputElement[];
    from.value = '2999-01-01';
    from.dispatchEvent(new Event('change'));
    TestBed.tick();
    fixture.detectChanges();

    backend.expectNone((r) => r.url.startsWith('/api/reports'));
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('on or before');
    expect(fixture.nativeElement.querySelector('button[mat-stroked-button]').disabled).toBe(true);
  });
});
