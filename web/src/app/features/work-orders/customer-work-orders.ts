import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { Router } from '@angular/router';
import { WorkOrderDialog, WorkOrderDialogData } from './work-order-dialog';
import { WorkOrderTable } from './work-order-table';
import { WorkOrder, WorkOrderListItem, WorkOrdersApi } from './work-orders.api';

/** The Work orders tab of a customer's page. */
@Component({
  selector: 'app-customer-work-orders',
  imports: [MatButtonModule, MatPaginatorModule, WorkOrderTable],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tab-actions">
      @if (customerActive()) {
        <button mat-flat-button (click)="create()">New work order</button>
      }
    </div>
    <app-work-order-table [items]="workOrders.value()?.items ?? []" [loaded]="workOrders.hasValue()" (open)="open($event)" />
    <mat-paginator [length]="workOrders.value()?.totalCount ?? 0" [pageSize]="20" [pageIndex]="page() - 1" (page)="setPage($event)" />
  `,
  styles: `.tab-actions { display: flex; justify-content: flex-end; margin: 12px 0; }`,
})
export class CustomerWorkOrders {
  private readonly api = inject(WorkOrdersApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly customerId = input.required<string>();
  readonly customerName = input.required<string>();
  readonly customerActive = input(true);
  protected readonly page = signal(1);
  protected readonly workOrders = rxResource({
    params: () => ({ customerId: this.customerId(), page: this.page() }),
    stream: ({ params }) =>
      this.api.list({ page: params.page, pageSize: 20, search: '', statuses: [], priority: null, type: null, customerId: params.customerId }),
  });

  protected setPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
  }

  protected open(row: WorkOrderListItem): void {
    this.router.navigate(['/office/work-orders', row.id]);
  }

  protected create(): void {
    this.dialog
      .open<WorkOrderDialog, WorkOrderDialogData, WorkOrder>(WorkOrderDialog, {
        data: { workOrder: null, customer: { id: this.customerId(), name: this.customerName() } },
      })
      .afterClosed()
      .subscribe((wo) => wo && this.router.navigate(['/office/work-orders', wo.id]));
  }
}
