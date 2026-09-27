import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EMPTY } from 'rxjs';
import { Realtime } from '../../core/realtime';
import { Dashboard } from './dashboard';
import { DashboardData, statusShares } from './dashboard.api';

describe('statusShares', () => {
  it('gives each non-empty status its share of open jobs', () => {
    expect(statusShares([{ status: 'New', count: 1 }, { status: 'Scheduled', count: 0 }, { status: 'InProgress', count: 3 }])).toEqual([
      { status: 'New', count: 1, percent: 25 },
      { status: 'InProgress', count: 3, percent: 75 },
    ]);
    expect(statusShares([{ status: 'New', count: 0 }])).toEqual([]);
  });
});

describe('Dashboard', () => {
  it('shows the figures, urgent jobs and who is on a job', async () => {
    TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Realtime, useValue: { workOrderChanges: () => EMPTY } },
      ],
    });
    const backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Dashboard);
    TestBed.tick();
    const data: DashboardData = {
      date: '2026-10-01',
      openByStatus: [{ status: 'New', count: 2 }, { status: 'InProgress', count: 1 }],
      unassigned: 2, overdue: 1, completedToday: 4, techniciansBusy: 1, techniciansFree: 2, techniciansOff: 1,
      technicians: [{ id: 't1', name: 'Sara Ali', color: '#1E88E5', state: 'Busy', currentJobId: 'w9', currentJobNumber: 'WO-000009', jobsToday: 3 }],
      urgentUnassigned: [{ id: 'w1', number: 'WO-000001', title: 'Chiller down', priority: 'Urgent', customerName: 'Al Noor',
        dueBy: '2026-10-01T05:00:00Z', isOverdue: true, createdAt: '2026-10-01T04:00:00Z' }],
    };
    backend.expectOne('/api/dashboard/today').flush(data);
    await fixture.whenStable();
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Completed today');
    expect(text).toContain('1 off today');
    expect(text).toContain('Overdue 1 Oct 2026, 09:00');
    const job = fixture.nativeElement.querySelector('a.state.busy') as HTMLAnchorElement;
    expect(job.textContent).toBe('WO-000009');
    expect(job.getAttribute('href')).toBe('/office/work-orders/w9');
    const newLink = [...fixture.nativeElement.querySelectorAll('.legend a')][0] as HTMLAnchorElement;
    expect(newLink.getAttribute('href')).toBe('/office/work-orders?status=New');
    backend.verify();
  });
});
