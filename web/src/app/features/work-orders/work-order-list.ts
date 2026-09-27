import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { statusLabel } from './work-order-labels';
import { WorkOrderDialog, WorkOrderDialogData } from './work-order-dialog';
import { onWorkOrderChange } from '../../core/realtime';
import { WorkOrderTable } from './work-order-table';
import {
  OPEN_STATUSES, PRIORITIES, STATUSES, TYPES, WorkOrder, WorkOrderListItem, WorkOrderPriority, WorkOrderQuery,
  WorkOrderStatus, WorkOrderType, WorkOrdersApi,
} from './work-orders.api';

@Component({
  selector: 'app-work-order-list',
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatPaginatorModule, WorkOrderTable],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Work orders</h1>
      <button mat-flat-button (click)="create()">New work order</button>
    </header>
    <div class="filters">
      <mat-form-field appearance="outline" class="search">
        <mat-label>Search number or title</mat-label>
        <input matInput (input)="set({ search: $any($event.target).value })" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Status</mat-label>
        <mat-select multiple [value]="query().statuses" (selectionChange)="set({ statuses: $event.value })">
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ label(s) }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Priority</mat-label>
        <mat-select [value]="query().priority" (selectionChange)="set({ priority: $event.value })">
          <mat-option [value]="null">Any</mat-option>
          @for (p of priorities; track p) {
            <mat-option [value]="p">{{ p }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Type</mat-label>
        <mat-select [value]="query().type" (selectionChange)="set({ type: $event.value })">
          <mat-option [value]="null">Any</mat-option>
          @for (t of types; track t) {
            <mat-option [value]="t">{{ t }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    <app-work-order-table [items]="workOrders.value()?.items ?? []" [loaded]="workOrders.hasValue()" (open)="openRow($event)" />
    <mat-paginator
      [length]="workOrders.value()?.totalCount ?? 0"
      [pageSize]="query().pageSize"
      [pageIndex]="query().page - 1"
      [pageSizeOptions]="[20, 50, 100]"
      (page)="page($event)" />
  `,
  styles: `
    .header { display: flex; align-items: center; justify-content: space-between; }
    .filters { display: flex; gap: 12px; flex-wrap: wrap; }
    .filters mat-form-field { width: 180px; }
    .filters .search { width: 280px; }
  `,
})
export class WorkOrderList {
  private readonly api = inject(WorkOrdersApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  protected readonly statuses = STATUSES;
  protected readonly priorities = PRIORITIES;
  protected readonly types = TYPES;
  protected readonly label = statusLabel;

  protected readonly query = signal<WorkOrderQuery>({
    page: 1,
    pageSize: 20,
    search: '',
    statuses: [...OPEN_STATUSES],
    priority: null as WorkOrderPriority | null,
    type: null as WorkOrderType | null,
  });
  protected readonly workOrders = rxResource({ params: () => this.query(), stream: ({ params }) => this.api.list(params) });
  constructor() {
    onWorkOrderChange(() => this.workOrders.reload());
  }

  protected set(change: Partial<{ search: string; statuses: WorkOrderStatus[]; priority: WorkOrderPriority | null; type: WorkOrderType | null }>): void {
    this.query.update((q) => ({ ...q, ...change, page: 1 }));
  }

  protected page(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected openRow(row: WorkOrderListItem): void {
    this.router.navigate(['/office/work-orders', row.id]);
  }

  protected create(): void {
    this.dialog
      .open<WorkOrderDialog, WorkOrderDialogData, WorkOrder>(WorkOrderDialog, { data: { workOrder: null } })
      .afterClosed()
      .subscribe((wo) => wo && this.router.navigate(['/office/work-orders', wo.id]));
  }
}
