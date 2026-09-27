import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { InvoiceStatus } from './invoices.api';

@Component({
  selector: 'app-invoice-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span [class]="'chip ' + (overdue() ? 'overdue' : status().toLowerCase())">{{ overdue() ? 'Overdue' : status() }}</span>`,
  styles: `
    .chip { display: inline-block; padding: 2px 10px; border-radius: 12px; font: var(--mat-sys-label-medium); white-space: nowrap;
      background: var(--mat-sys-surface-container-high); color: var(--mat-sys-on-surface); }
    .issued { background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
    .paid { background: #d7f0dc; color: #0f5223; }
    .void { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .overdue { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
  `,
})
export class InvoiceStatusChip {
  readonly status = input.required<InvoiceStatus>();
  readonly overdue = input(false);
}
