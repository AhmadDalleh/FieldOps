import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { formatDubai } from '../../../shared/time/dubai-time';
import { PriorityChip, StatusChip } from '../../work-orders/work-order-labels';
import { FieldApi, JobDay } from '../field.api';
import { onWorkOrderChange } from '../../../core/realtime';

/** The Dubai clock time of an instant, e.g. `14:30`. */
export function clockTime(iso: string | null): string {
  return iso ? formatDubai(iso).slice(-5) : '';
}

/** US-TAPP-01: today's (or tomorrow's) jobs in start order, sized for a phone. */
@Component({
  selector: 'app-my-jobs',
  imports: [RouterLink, MatButtonModule, MatButtonToggleModule, MatIconModule, StatusChip, PriorityChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>My jobs</h1>
      <mat-button-toggle-group aria-label="Day" [value]="day()" (change)="day.set($event.value)" hideSingleSelectionIndicator>
        <mat-button-toggle value="Today">Today</mat-button-toggle>
        <mat-button-toggle value="Tomorrow">Tomorrow</mat-button-toggle>
      </mat-button-toggle-group>
    </header>

    @if (jobs.error()) {
      <p class="error">Could not load your jobs. <button mat-button (click)="jobs.reload()">Try again</button></p>
    }
    @for (j of jobs.value() ?? []; track j.id) {
      <a class="card" [routerLink]="['/tech/jobs', j.id]">
        <div class="top">
          <span class="time">{{ time(j.scheduledStart) }}–{{ time(j.scheduledEnd) }}</span>
          <app-status-chip [status]="j.status" />
        </div>
        <div class="title">{{ j.title }}</div>
        <div class="muted">{{ j.customerName }}</div>
        <div class="muted">{{ j.siteName }} · {{ j.siteCity }}</div>
        <div class="bottom">
          <span class="muted small">{{ j.number }} · {{ j.type }}</span>
          <app-priority-chip [priority]="j.priority" />
        </div>
      </a>
    } @empty {
      @if (jobs.hasValue()) {
        <div class="empty">
          <mat-icon>event_available</mat-icon>
          <p>No jobs {{ day() === 'Today' ? 'today' : 'tomorrow' }}.</p>
        </div>
      }
    }
  `,
  styles: `
    .header { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
    .header h1 { margin: 0; }
    .card { display: block; padding: 12px; margin-bottom: 12px; border-radius: 12px; text-decoration: none; color: inherit;
      border: 1px solid var(--mat-sys-outline-variant); background: var(--mat-sys-surface); }
    .top, .bottom { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .time { font: var(--mat-sys-title-medium); }
    .title { font: var(--mat-sys-body-large); margin: 6px 0 2px; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .small { font: var(--mat-sys-body-small); }
    .bottom { margin-top: 6px; }
    .empty { display: flex; flex-direction: column; align-items: center; padding: 48px 0; color: var(--mat-sys-on-surface-variant); }
    .empty mat-icon { font-size: 48px; width: 48px; height: 48px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class MyJobs {
  private readonly api = inject(FieldApi);
  protected readonly day = signal<JobDay>('Today');
  protected readonly jobs = rxResource({ params: () => this.day(), stream: ({ params }) => this.api.myJobs(params) });
  constructor() {
    onWorkOrderChange(() => this.jobs.reload());
  }
  protected readonly time = clockTime;
}
