import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { interval } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { onWorkOrderChange } from '../../core/realtime';
import { DubaiTimePipe } from '../../shared/time/dubai-time.pipe';
import { STATUS_COLORS } from '../dispatch/board-layout';
import { statusLabel } from '../work-orders/work-order-labels';
import { DashboardApi, TechnicianState, statusShares } from './dashboard.api';

const STATE_LABELS: Record<TechnicianState, string> = { Busy: 'On a job', Free: 'Free', Off: 'Off today' };

/** US-DSH-01: today at a glance for the office; refreshes live and every minute (for overdue). */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, MatCardModule, MatIconModule, MatButtonModule, DubaiTimePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="head">
      <h1>Today</h1>
      <span class="who">{{ auth.user()?.fullName }}</span>
    </header>

    @if (data.error()) {
      <p class="error" role="alert">Could not load the dashboard. <button mat-button (click)="data.reload()">Try again</button></p>
    }
    @if (data.value(); as d) {
      <section class="kpis" aria-label="Key figures">
        <a class="kpi" routerLink="/office/dispatch" [class.warn]="d.unassigned > 0">
          <span class="value">{{ d.unassigned }}</span><span class="name">Unassigned</span>
        </a>
        <a class="kpi" routerLink="/office/work-orders" [class.bad]="d.overdue > 0">
          <span class="value">{{ d.overdue }}</span><span class="name">Overdue</span>
        </a>
        <div class="kpi good">
          <span class="value">{{ d.completedToday }}</span><span class="name">Completed today</span>
        </div>
        <div class="kpi">
          <span class="value">{{ d.techniciansBusy }} / {{ d.techniciansFree }}</span>
          <span class="name">Technicians busy / free</span>
          @if (d.techniciansOff) {
            <span class="note">{{ d.techniciansOff }} off today</span>
          }
        </div>
      </section>

      <mat-card appearance="outlined" class="pipeline">
        <mat-card-header><mat-card-title>Open jobs by status</mat-card-title></mat-card-header>
        <mat-card-content>
          @if (shares().length) {
            <div class="bar" aria-hidden="true">
              @for (s of shares(); track s.status) {
                <span [style.width.%]="s.percent" [style.background]="color(s.status)"></span>
              }
            </div>
          }
          <ul class="legend">
            @for (s of d.openByStatus; track s.status) {
              <li>
                <a [routerLink]="['/office/work-orders']" [queryParams]="{ status: s.status }">
                  <span class="dot" [style.background]="color(s.status)"></span>{{ label(s.status) }}
                  <strong>{{ s.count }}</strong>
                </a>
              </li>
            }
          </ul>
        </mat-card-content>
      </mat-card>

      <div class="columns">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Urgent and unassigned</mat-card-title></mat-card-header>
          <mat-card-content>
            @for (j of d.urgentUnassigned; track j.id) {
              <a class="row" [routerLink]="['/office/work-orders', j.id]">
                <span class="main"><strong>{{ j.number }}</strong> {{ j.title }}<small>{{ j.customerName }}</small></span>
                @if (j.dueBy) {
                  <span class="due" [class.bad]="j.isOverdue">{{ j.isOverdue ? 'Overdue' : 'Due' }} {{ j.dueBy | dubaiTime }}</span>
                }
              </a>
            } @empty {
              <p class="empty">No urgent jobs are waiting.</p>
            }
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Technicians</mat-card-title></mat-card-header>
          <mat-card-content>
            @for (t of d.technicians; track t.id) {
              <div class="row">
                <span class="main"><span class="dot" [style.background]="t.color"></span>{{ t.name }}
                  <small>{{ t.jobsToday }} {{ t.jobsToday === 1 ? 'job' : 'jobs' }} today</small></span>
                @if (t.currentJobId) {
                  <a class="state busy" [routerLink]="['/office/work-orders', t.currentJobId]">{{ t.currentJobNumber }}</a>
                } @else {
                  <span class="state" [class.off]="t.state === 'Off'">{{ stateLabel(t.state) }}</span>
                }
              </div>
            } @empty {
              <p class="empty">No active technicians.</p>
            }
          </mat-card-content>
        </mat-card>
      </div>
    } @else if (data.isLoading()) {
      <p>Loading…</p>
    }
  `,
  styles: `
    .head { display: flex; align-items: baseline; gap: 16px; }
    .head h1 { margin: 0 0 16px; }
    .who { color: var(--mat-sys-on-surface-variant); }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 16px; }
    .kpi { display: flex; flex-direction: column; padding: 16px; border-radius: 12px; background: var(--mat-sys-surface-container);
      color: inherit; text-decoration: none; }
    .kpi .value { font: var(--mat-sys-display-small); }
    .kpi .name { font: var(--mat-sys-label-large); color: var(--mat-sys-on-surface-variant); }
    .kpi .note { font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
    .kpi.warn { background: #fff3e0; }
    .kpi.bad { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .kpi.good { background: #e8f5e9; }
    .pipeline { margin-bottom: 16px; }
    .bar { display: flex; height: 14px; border-radius: 7px; overflow: hidden; margin: 8px 0 12px; }
    .legend { display: flex; flex-wrap: wrap; gap: 8px 24px; list-style: none; margin: 0; padding: 0; }
    .legend a { display: inline-flex; align-items: center; gap: 6px; color: inherit; text-decoration: none; }
    .dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 6px; flex: none; }
    .columns { display: grid; grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); gap: 16px; }
    .row { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 10px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant); color: inherit; text-decoration: none; }
    .row:last-child { border-bottom: 0; }
    .main { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 8px; }
    .main small { flex-basis: 100%; color: var(--mat-sys-on-surface-variant); }
    .due { white-space: nowrap; font: var(--mat-sys-label-medium); }
    .bad { color: var(--mat-sys-error); }
    .kpi.bad .value, .kpi.bad .name { color: inherit; }
    .state { white-space: nowrap; font: var(--mat-sys-label-medium); color: #2e7d32; }
    .state.busy { color: #ef6c00; }
    .state.off { color: var(--mat-sys-on-surface-variant); }
    .empty { color: var(--mat-sys-on-surface-variant); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class Dashboard {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(DashboardApi);
  protected readonly data = rxResource({ stream: () => this.api.today() });
  protected readonly shares = computed(() => statusShares(this.data.value()?.openByStatus ?? []));
  protected readonly label = statusLabel;
  protected readonly color = (status: string) => STATUS_COLORS[status];
  protected readonly stateLabel = (state: TechnicianState) => STATE_LABELS[state];

  constructor() {
    onWorkOrderChange(() => this.data.reload());
    interval(60_000).pipe(takeUntilDestroyed()).subscribe(() => this.data.reload());
  }
}
