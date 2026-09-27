import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { problemMessage } from '../../core/problem';
import { dubaiToday } from '../../shared/time/dubai-time';
import { DubaiTimePipe } from '../../shared/time/dubai-time.pipe';
import { TechniciansApi } from '../technicians/technicians.api';
import { TimeOffDialog } from './time-off-dialog';
import { TimeOff, TimeOffApi, TimeOffStatus } from './time-off.api';

@Component({
  selector: 'app-time-off-approvals',
  imports: [MatTableModule, MatButtonModule, MatButtonToggleModule, DubaiTimePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Time off</h1>
      <button mat-flat-button (click)="add()">Add time off</button>
    </header>
    <mat-button-toggle-group [value]="status()" (change)="status.set($event.value)" aria-label="Status">
      <mat-button-toggle value="Pending">Pending</mat-button-toggle>
      <mat-button-toggle value="Approved">Approved</mat-button-toggle>
      <mat-button-toggle value="Rejected">Rejected</mat-button-toggle>
      <mat-button-toggle [value]="null">All</mat-button-toggle>
    </mat-button-toggle-group>

    <table mat-table [dataSource]="requests.value() ?? []">
      <ng-container matColumnDef="technician">
        <th mat-header-cell *matHeaderCellDef>Technician</th>
        <td mat-cell *matCellDef="let r">{{ r.technicianName }}</td>
      </ng-container>
      <ng-container matColumnDef="period">
        <th mat-header-cell *matHeaderCellDef>Period (Dubai time)</th>
        <td mat-cell *matCellDef="let r">{{ r.startsAt | dubaiTime }} to {{ r.endsAt | dubaiTime }}</td>
      </ng-container>
      <ng-container matColumnDef="reason">
        <th mat-header-cell *matHeaderCellDef>Reason</th>
        <td mat-cell *matCellDef="let r">{{ r.reason }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>Status</th>
        <td mat-cell *matCellDef="let r">{{ r.status }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let r" class="actions">
          @if (r.status === 'Pending') {
            <button mat-button (click)="decide(r, true)">Approve</button>
            <button mat-button (click)="decide(r, false)">Reject</button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if (requests.hasValue() && requests.value().length === 0) {
      <p class="muted empty">No requests.</p>
    }
  `,
  styles: `
    .header { display: flex; align-items: center; justify-content: space-between; }
    mat-button-toggle-group { margin-bottom: 16px; }
    .actions { text-align: right; white-space: nowrap; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .empty { padding: 16px; }
  `,
})
export class TimeOffApprovals {
  private readonly api = inject(TimeOffApi);
  private readonly technicians = inject(TechniciansApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly columns = ['technician', 'period', 'reason', 'status', 'actions'];
  protected readonly status = signal<TimeOffStatus | null>('Pending');
  protected readonly requests = rxResource({
    params: () => ({ status: this.status() }),
    stream: ({ params }) => this.api.list(params.status),
  });

  protected decide(request: TimeOff, approve: boolean): void {
    this.api.decide(request.id, approve).subscribe({
      next: (decision) => {
        const conflicts = decision.conflictingJobs;
        const message = conflicts.length
          ? `Approved. Reschedule ${conflicts.map((j) => j.number).join(', ')}.`
          : `Time off ${approve ? 'approved' : 'rejected'}.`;
        this.snackBar.open(message, 'Close', { duration: conflicts.length ? 0 : 4000 });
        this.requests.reload();
      },
      error: (err: unknown) => this.snackBar.open(problemMessage(err), 'Close', { duration: 5000 }),
    });
  }

  protected add(): void {
    this.technicians.list(dubaiToday()).subscribe((rows) => {
      const technicians = rows.map((r) => ({ id: r.technician.id, name: r.technician.fullName }));
      this.dialog
        .open(TimeOffDialog, { data: { technicians } })
        .afterClosed()
        .subscribe((saved) => saved && this.requests.reload());
    });
  }
}
