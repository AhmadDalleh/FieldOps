import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { problemMessage } from '../../core/problem';
import { InvoiceStatusChip } from './invoice-labels';
import { INVOICE_STATUSES, InvoiceQuery, InvoicesApi, formatDay, invoiceName } from './invoices.api';

/** US-BIL-07: invoices by status, customer and date, with overdue ones flagged. */
@Component({
  selector: 'app-invoice-list',
  imports: [DecimalPipe, RouterLink, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatSlideToggleModule, MatButtonModule, MatIconModule, InvoiceStatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Invoices</h1>
    <div class="filters">
      <mat-form-field appearance="outline" class="search">
        <mat-label>Search number, job or customer</mat-label>
        <input matInput (input)="patch({ search: $any($event.target).value })" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Status</mat-label>
        <mat-select [value]="query().status" (selectionChange)="patch({ status: $event.value })">
          <mat-option [value]="null">Any</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ s }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>From</mat-label>
        <input matInput type="date" (change)="patch({ from: $any($event.target).value || null })" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>To</mat-label>
        <input matInput type="date" (change)="patch({ to: $any($event.target).value || null })" />
      </mat-form-field>
      <mat-slide-toggle [checked]="query().overdue" (change)="patch({ overdue: $event.checked })">Overdue only</mat-slide-toggle>
      @if (query().customerId) {
        <button mat-stroked-button (click)="clearCustomer()" aria-label="Show all customers">
          {{ customerLabel() }}<mat-icon iconPositionEnd>close</mat-icon>
        </button>
      }
    </div>

    @if (invoices.error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    <div class="scroll">
      <table mat-table [dataSource]="invoices.value()?.items ?? []">
        <ng-container matColumnDef="number">
          <th mat-header-cell *matHeaderCellDef>Invoice</th>
          <td mat-cell *matCellDef="let i"><a [routerLink]="['/office/invoices', i.id]">{{ name(i) }}</a></td>
        </ng-container>
        <ng-container matColumnDef="customer">
          <th mat-header-cell *matHeaderCellDef>Customer</th>
          <td mat-cell *matCellDef="let i">{{ i.customerName }}</td>
        </ng-container>
        <ng-container matColumnDef="job">
          <th mat-header-cell *matHeaderCellDef>Job</th>
          <td mat-cell *matCellDef="let i"><a [routerLink]="['/office/work-orders', i.workOrderId]">{{ i.workOrderNumber }}</a></td>
        </ng-container>
        <ng-container matColumnDef="issued">
          <th mat-header-cell *matHeaderCellDef>Issued</th>
          <td mat-cell *matCellDef="let i">{{ day(i.issueDate) }}</td>
        </ng-container>
        <ng-container matColumnDef="due">
          <th mat-header-cell *matHeaderCellDef>Due</th>
          <td mat-cell *matCellDef="let i" [class.overdue]="i.isOverdue">{{ day(i.dueDate) }}</td>
        </ng-container>
        <ng-container matColumnDef="total">
          <th mat-header-cell *matHeaderCellDef class="num">Total</th>
          <td mat-cell *matCellDef="let i" class="num">{{ i.total | number: '1.2-2' }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Status</th>
          <td mat-cell *matCellDef="let i"><app-invoice-status [status]="i.status" [overdue]="i.isOverdue" /></td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
        <tr class="mat-row" *matNoDataRow><td class="mat-cell muted" [attr.colspan]="columns.length">No invoices match.</td></tr>
      </table>
    </div>
    <mat-paginator [length]="invoices.value()?.totalCount ?? 0" [pageSize]="query().pageSize" [pageIndex]="query().page - 1"
      [pageSizeOptions]="[20, 50, 100]" (page)="page($event)" />
  `,
  styles: `
    .filters { display: flex; gap: 12px; align-items: baseline; flex-wrap: wrap; }
    .search { min-width: 280px; }
    .scroll { overflow-x: auto; }
    .num { text-align: right; }
    .overdue { color: var(--mat-sys-error); font-weight: 600; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class InvoiceList {
  private readonly api = inject(InvoicesApi);
  private readonly router = inject(Router);
  protected readonly statuses = INVOICE_STATUSES;
  protected readonly columns = ['number', 'customer', 'job', 'issued', 'due', 'total', 'status'];
  protected readonly name = invoiceName;
  protected readonly day = formatDay;

  private readonly params = inject(ActivatedRoute).snapshot.queryParamMap;
  protected readonly query = signal<InvoiceQuery>({
    page: 1,
    pageSize: 20,
    search: '',
    status: null,
    customerId: this.params.get('customerId'),
    from: null,
    to: null,
    overdue: this.params.get('overdue') === 'true',
  });
  protected readonly invoices = rxResource({ params: () => this.query(), stream: ({ params }) => this.api.list(params) });
  protected readonly error = computed(() => problemMessage(this.invoices.error()));
  protected readonly customerLabel = computed(() => {
    const first = this.invoices.value()?.items[0];
    return first && first.customerId === this.query().customerId ? `${first.customerName} only` : 'One customer only';
  });

  protected patch(change: Partial<InvoiceQuery>): void {
    this.query.update((q) => ({ ...q, page: 1, ...change }));
  }

  protected page(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected clearCustomer(): void {
    this.patch({ customerId: null });
    void this.router.navigate([], { queryParams: {} });
  }
}
