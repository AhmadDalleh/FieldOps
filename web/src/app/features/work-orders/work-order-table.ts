import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { DubaiTimePipe } from '../../shared/time/dubai-time.pipe';
import { PriorityChip, StatusChip } from './work-order-labels';
import { WorkOrderListItem } from './work-orders.api';

/** The work order table shared by the work order list and a customer's Work orders tab. */
@Component({
  selector: 'app-work-order-table',
  imports: [MatTableModule, RouterLink, DubaiTimePipe, StatusChip, PriorityChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <table mat-table [dataSource]="items()">
      <ng-container matColumnDef="number">
        <th mat-header-cell *matHeaderCellDef>Number</th>
        <td mat-cell *matCellDef="let w"><a [routerLink]="['/office/work-orders', w.id]">{{ w.number }}</a></td>
      </ng-container>
      <ng-container matColumnDef="title">
        <th mat-header-cell *matHeaderCellDef>Title</th>
        <td mat-cell *matCellDef="let w">
          {{ w.title }}
          <div class="muted">{{ w.type }}</div>
        </td>
      </ng-container>
      <ng-container matColumnDef="customer">
        <th mat-header-cell *matHeaderCellDef>Customer / site</th>
        <td mat-cell *matCellDef="let w">{{ w.customerName }}<div class="muted">{{ w.siteName }}</div></td>
      </ng-container>
      <ng-container matColumnDef="priority">
        <th mat-header-cell *matHeaderCellDef>Priority</th>
        <td mat-cell *matCellDef="let w"><app-priority-chip [priority]="w.priority" /></td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>Status</th>
        <td mat-cell *matCellDef="let w"><app-status-chip [status]="w.status" /></td>
      </ng-container>
      <ng-container matColumnDef="technician">
        <th mat-header-cell *matHeaderCellDef>Technician</th>
        <td mat-cell *matCellDef="let w">{{ w.technicianName || '—' }}</td>
      </ng-container>
      <ng-container matColumnDef="due">
        <th mat-header-cell *matHeaderCellDef>Due by</th>
        <td mat-cell *matCellDef="let w" [class.overdue]="w.isOverdue">
          {{ w.dueBy ? (w.dueBy | dubaiTime) : '—' }}
          @if (w.isOverdue) {
            <div class="overdue-flag">Overdue</div>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns" (click)="open.emit(row)" class="clickable"></tr>
    </table>
    @if (loaded() && items().length === 0) {
      <p class="muted empty">No work orders match.</p>
    }
  `,
  styles: `
    .muted { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .overdue { color: var(--mat-sys-error); }
    .overdue-flag { font: var(--mat-sys-label-small); font-weight: 700; text-transform: uppercase; }
    .clickable { cursor: pointer; }
    .clickable:hover { background: var(--mat-sys-surface-container-low); }
    .empty { padding: 16px; }
  `,
})
export class WorkOrderTable {
  readonly items = input.required<WorkOrderListItem[]>();
  readonly loaded = input(true);
  readonly open = output<WorkOrderListItem>();
  protected readonly columns = ['number', 'title', 'customer', 'priority', 'status', 'technician', 'due'];
}
