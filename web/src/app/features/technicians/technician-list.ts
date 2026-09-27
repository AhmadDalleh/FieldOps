import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../core/auth.service';
import { dubaiToday } from '../../shared/time/dubai-time';
import { DubaiTimePipe } from '../../shared/time/dubai-time.pipe';
import { TechnicianDialog } from './technician-dialog';
import { Technician, TechniciansApi } from './technicians.api';

@Component({
  selector: 'app-technician-list',
  imports: [MatTableModule, MatButtonModule, MatChipsModule, MatFormFieldModule, MatInputModule, MatSlideToggleModule, DubaiTimePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Technicians</h1>
    </header>
    <div class="filters">
      <mat-form-field appearance="outline">
        <mat-label>Availability on</mat-label>
        <input matInput type="date" [value]="query().date" (change)="setDate($any($event.target).value)" />
      </mat-form-field>
      <mat-slide-toggle [checked]="query().includeInactive" (change)="setIncludeInactive($event.checked)">Show inactive</mat-slide-toggle>
    </div>

    <table mat-table [dataSource]="rows()">
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>Technician</th>
        <td mat-cell *matCellDef="let r">
          <span class="swatch" [style.background]="r.technician.color"></span>
          {{ r.technician.fullName }}
          <div class="muted">{{ r.technician.employeeCode }} · {{ r.technician.phone || 'No phone' }}</div>
        </td>
      </ng-container>
      <ng-container matColumnDef="skills">
        <th mat-header-cell *matHeaderCellDef>Skills</th>
        <td mat-cell *matCellDef="let r">
          <mat-chip-set>
            @for (skill of r.technician.skills; track skill.id) {
              <mat-chip>{{ skill.name }}</mat-chip>
            } @empty {
              <span class="muted">None</span>
            }
          </mat-chip-set>
        </td>
      </ng-container>
      <ng-container matColumnDef="hours">
        <th mat-header-cell *matHeaderCellDef>Hours</th>
        <td mat-cell *matCellDef="let r">{{ r.technician.workingHoursStart.slice(0, 5) }}–{{ r.technician.workingHoursEnd.slice(0, 5) }}</td>
      </ng-container>
      <ng-container matColumnDef="jobs">
        <th mat-header-cell *matHeaderCellDef>Jobs</th>
        <td mat-cell *matCellDef="let r">{{ r.jobCount }}</td>
      </ng-container>
      <ng-container matColumnDef="availability">
        <th mat-header-cell *matHeaderCellDef>Availability</th>
        <td mat-cell *matCellDef="let r">
          @if (!r.technician.isActive) {
            <span class="badge off">Inactive</span>
          } @else if (r.isAvailable) {
            <span class="badge ok">Available</span>
          } @else {
            <span class="badge off">Time off</span>
          }
          @for (slot of r.timeOff; track slot.id) {
            <div class="muted">{{ slot.startsAt | dubaiTime }} to {{ slot.endsAt | dubaiTime }}</div>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let r" class="actions">
          @if (isAdmin()) {
            <button mat-button (click)="edit(r.technician)">Edit</button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if (technicians.hasValue() && rows().length === 0) {
      <p class="muted empty">No technicians yet. Add a user with the Technician role to create one.</p>
    }
  `,
  styles: `
    .filters { display: flex; gap: 24px; align-items: center; flex-wrap: wrap; }
    .swatch { display: inline-block; width: 12px; height: 12px; border-radius: 50%; margin-right: 6px; vertical-align: middle; }
    .muted { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .badge { padding: 2px 8px; border-radius: 12px; font: var(--mat-sys-label-medium); }
    .badge.ok { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .badge.off { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .actions { text-align: right; }
    .empty { padding: 16px; }
  `,
})
export class TechnicianList {
  private readonly api = inject(TechniciansApi);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  protected readonly columns = ['name', 'skills', 'hours', 'jobs', 'availability', 'actions'];
  protected readonly query = signal({ date: dubaiToday(), includeInactive: false });
  protected readonly technicians = rxResource({
    params: () => this.query(),
    stream: ({ params }) => this.api.list(params.date, params.includeInactive),
  });
  protected readonly rows = computed(() => this.technicians.value() ?? []);

  protected setDate(date: string): void {
    if (date) this.query.update((q) => ({ ...q, date }));
  }

  protected setIncludeInactive(includeInactive: boolean): void {
    this.query.update((q) => ({ ...q, includeInactive }));
  }

  protected edit(technician: Technician): void {
    this.dialog
      .open(TechnicianDialog, { data: technician })
      .afterClosed()
      .subscribe((saved) => saved && this.technicians.reload());
  }
}
