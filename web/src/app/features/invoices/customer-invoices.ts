import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { InvoiceStatusChip } from './invoice-labels';
import { InvoicesApi, formatDay, invoiceName } from './invoices.api';

/** The Invoices tab on a customer: their latest invoices, with a link to the full filtered list. */
@Component({
  selector: 'app-customer-invoices',
  imports: [DecimalPipe, RouterLink, MatButtonModule, InvoiceStatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (i of invoices.value()?.items ?? []; track i.id) {
      <div class="row">
        <a [routerLink]="['/office/invoices', i.id]">{{ name(i) }}</a>
        <span class="muted">{{ i.workOrderNumber }}</span>
        <span class="muted">{{ day(i.issueDate) }}</span>
        <span class="num">{{ i.total | number: '1.2-2' }}</span>
        <app-invoice-status [status]="i.status" [overdue]="i.isOverdue" />
      </div>
    } @empty {
      <p class="muted">{{ invoices.isLoading() ? 'Loading…' : 'No invoices yet.' }}</p>
    }
    @if ((invoices.value()?.totalCount ?? 0) > 10) {
      <a mat-button routerLink="/office/invoices" [queryParams]="{ customerId: customerId() }">All {{ invoices.value()?.totalCount }} invoices</a>
    }
  `,
  styles: `
    :host { display: block; padding: 16px 0; }
    .row { display: grid; grid-template-columns: 1.2fr 1fr 1fr 1fr auto; gap: 12px; align-items: center; padding: 8px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .num { text-align: right; }
    .muted { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class CustomerInvoices {
  private readonly api = inject(InvoicesApi);
  readonly customerId = input.required<string>();
  protected readonly name = invoiceName;
  protected readonly day = formatDay;
  protected readonly invoices = rxResource({
    params: () => this.customerId(),
    stream: ({ params }) =>
      this.api.list({ page: 1, pageSize: 10, search: '', status: null, customerId: params, from: null, to: null, overdue: false }),
  });
}
