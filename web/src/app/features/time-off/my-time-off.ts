import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { DubaiTimePipe } from '../../shared/time/dubai-time.pipe';
import { TimeOffDialog } from './time-off-dialog';
import { TimeOffApi } from './time-off.api';

/** The technician's own time-off requests, in the mobile shell. */
@Component({
  selector: 'app-my-time-off',
  imports: [MatButtonModule, MatCardModule, DubaiTimePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Time off</h1>
      <button mat-flat-button (click)="request()">Request</button>
    </header>
    @for (r of requests.value() ?? []; track r.id) {
      <mat-card appearance="outlined" class="card">
        <mat-card-content>
          <div class="top">
            <strong>{{ r.startsAt | dubaiTime }}</strong>
            <span class="status" [class]="r.status.toLowerCase()">{{ r.status }}</span>
          </div>
          <div>to {{ r.endsAt | dubaiTime }}</div>
          @if (r.reason) {
            <div class="muted">{{ r.reason }}</div>
          }
        </mat-card-content>
      </mat-card>
    } @empty {
      @if (requests.hasValue()) {
        <p class="muted">You have no time-off requests.</p>
      }
    }
  `,
  styles: `
    .header { display: flex; align-items: center; justify-content: space-between; }
    .card { margin-bottom: 12px; }
    .top { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .status { padding: 2px 8px; border-radius: 12px; font: var(--mat-sys-label-medium); background: var(--mat-sys-surface-container-high); }
    .status.approved { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .status.rejected { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .muted { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class MyTimeOff {
  private readonly api = inject(TimeOffApi);
  private readonly dialog = inject(MatDialog);
  protected readonly requests = rxResource({ stream: () => this.api.list() });

  protected request(): void {
    this.dialog
      .open(TimeOffDialog, { data: {}, width: '100%', maxWidth: '480px' })
      .afterClosed()
      .subscribe((saved) => saved && this.requests.reload());
  }
}
