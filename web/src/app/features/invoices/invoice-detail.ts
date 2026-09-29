import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { Observable, filter, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { problemMessage } from '../../core/problem';
import { saveBlob } from '../../shared/files/download';
import { ConfirmData, ConfirmDialog } from '../../shared/ui/confirm-dialog/confirm-dialog';
import { ReasonDialog, ReasonDialogData } from '../work-orders/reason-dialog';
import { InvoiceStatusChip } from './invoice-labels';
import { Invoice, InvoiceLine, InvoicesApi, formatDay, invoiceName } from './invoices.api';
import { LineDialog, LineDialogData } from './line-dialog';
import { MarkPaidDialog } from './mark-paid-dialog';

/** US-BIL-02..06: one invoice, editable while a draft; issue, mark paid and void are for admins. */
@Component({
  selector: 'app-invoice-detail',
  imports: [DecimalPipe, RouterLink, MatButtonModule, MatCardModule, MatIconModule, InvoiceStatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/office/invoices" class="back">← Invoices</a>
    @if (invoice(); as i) {
      <header class="header">
        <div>
          <h1>{{ name(i) }}</h1>
          <div class="meta">
            <app-invoice-status [status]="i.status" [overdue]="i.isOverdue" />
            <span>{{ i.customerName }}</span>
            <a [routerLink]="['/office/work-orders', i.workOrderId]">{{ i.workOrderNumber }} · {{ i.workOrderTitle }}</a>
          </div>
        </div>
        <div class="actions">
          <button mat-stroked-button (click)="download(i)" [disabled]="busy()"><mat-icon>picture_as_pdf</mat-icon>PDF</button>
          @if (i.status === 'Draft') {
            <button mat-button class="danger" (click)="remove(i)" [disabled]="busy()">Delete draft</button>
            @if (isAdmin()) {
              <button mat-flat-button (click)="issue(i)" [disabled]="busy() || i.lines.length === 0">Issue</button>
            }
          }
          @if (i.status === 'Issued' && isAdmin()) {
            <button mat-button class="danger" (click)="voidInvoice(i)" [disabled]="busy()">Void</button>
            <button mat-flat-button (click)="markPaid(i)" [disabled]="busy()">Mark paid</button>
          }
        </div>
      </header>

      @if (i.status === 'Draft' && !isAdmin()) {
        <p class="note">An admin issues the invoice once the lines are right.</p>
      }
      @if (i.status === 'Void') {
        <p class="note void">Voided: {{ i.voidReason }}</p>
      }

      <mat-card appearance="outlined">
        <mat-card-content>
          <dl class="facts">
            <div><dt>Issued</dt><dd>{{ day(i.issueDate) || '—' }}</dd></div>
            <div><dt>Due</dt><dd [class.overdue]="i.isOverdue">{{ day(i.dueDate) || '—' }}</dd></div>
            @if (i.status === 'Paid') {
              <div><dt>Paid</dt><dd>{{ day(i.paidAt) }} · {{ i.paymentReference }}</dd></div>
            }
          </dl>

          <table class="lines">
            <thead>
              <tr>
                <th>Description</th>
                <th class="num">Qty</th>
                <th class="num">Unit price</th>
                <th class="num">Amount ({{ i.currency }})</th>
                @if (i.status === 'Draft') {
                  <th></th>
                }
              </tr>
            </thead>
            <tbody>
              @for (l of i.lines; track l.id) {
                <tr>
                  <td>{{ l.description }} <span class="muted small">{{ l.lineType }}</span></td>
                  <td class="num">{{ l.quantity | number: '1.0-2' }}</td>
                  <td class="num">{{ l.unitPrice | number: '1.2-2' }}</td>
                  <td class="num">{{ l.lineTotal | number: '1.2-2' }}</td>
                  @if (i.status === 'Draft') {
                    <td class="num">
                      <button mat-icon-button [attr.aria-label]="'Edit ' + l.description" (click)="editLine(i, l)"><mat-icon>edit</mat-icon></button>
                      <button mat-icon-button [attr.aria-label]="'Remove ' + l.description" (click)="removeLine(i, l)"><mat-icon>delete</mat-icon></button>
                    </td>
                  }
                </tr>
              } @empty {
                <tr><td class="muted" colspan="5">No lines yet. No work time or parts were recorded on the job.</td></tr>
              }
            </tbody>
            <tfoot>
              <tr><td colspan="3">Subtotal</td><td class="num">{{ i.subtotal | number: '1.2-2' }}</td></tr>
              <tr><td colspan="3">VAT {{ i.vatRate | number: '1.0-2' }}%</td><td class="num">{{ i.vatAmount | number: '1.2-2' }}</td></tr>
              <tr class="total"><td colspan="3">Total</td><td class="num">{{ i.currency }} {{ i.total | number: '1.2-2' }}</td></tr>
            </tfoot>
          </table>
          @if (i.status === 'Draft') {
            <button mat-stroked-button (click)="editLine(i, null)"><mat-icon>add</mat-icon>Add line</button>
          }
        </mat-card-content>
      </mat-card>
    } @else if (loaded.error()) {
      <p class="error" role="alert">{{ errorMessage() }}</p>
    } @else {
      <p class="muted">Loading…</p>
    }
  `,
  styles: `
    .back { display: inline-block; margin-bottom: 8px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 12px; margin-bottom: 12px; }
    .header h1 { margin: 0 0 6px; }
    .meta { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .danger { color: var(--mat-sys-error); }
    .note { padding: 8px 12px; border-radius: 8px; background: var(--mat-sys-surface-container); }
    .note.void { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .facts { display: flex; gap: 32px; margin: 0 0 16px; }
    .facts dt { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }
    .facts dd { margin: 0; }
    .overdue { color: var(--mat-sys-error); font-weight: 600; }
    .lines { width: 100%; border-collapse: collapse; margin-bottom: 12px; }
    .lines th, .lines td { padding: 8px; border-bottom: 1px solid var(--mat-sys-outline-variant); text-align: left; }
    .lines th { font: var(--mat-sys-title-small); color: var(--mat-sys-on-surface-variant); }
    .lines tfoot td { border-bottom: none; padding: 4px 8px; }
    .lines tfoot td:first-child { text-align: right; }
    .total td { font-weight: 700; font-size: 1.1em; }
    .num { text-align: right !important; white-space: nowrap; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .small { font: var(--mat-sys-label-small); margin-left: 6px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class InvoiceDetail {
  private readonly api = inject(InvoicesApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  readonly id = input.required<string>();

  protected readonly loaded = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });
  protected readonly invoice = linkedSignal<Invoice | null>(() => this.loaded.value() ?? null);
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  protected readonly errorMessage = computed(() => problemMessage(this.loaded.error()));
  protected readonly busy = signal(false);
  protected readonly name = invoiceName;
  protected readonly day = formatDay;

  protected editLine(i: Invoice, line: InvoiceLine | null): void {
    this.dialog
      .open<LineDialog, LineDialogData, Invoice>(LineDialog, { data: { invoice: i, line } })
      .afterClosed()
      .subscribe((updated) => updated && this.invoice.set(updated));
  }

  protected removeLine(i: Invoice, line: InvoiceLine): void {
    this.run(this.api.removeLine(i.id, line.id));
  }

  protected issue(i: Invoice): void {
    this.confirm({
      title: 'Issue this invoice?',
      message: `It gets the next invoice number, becomes read-only, and ${i.workOrderNumber} moves to Invoiced.`,
      confirmLabel: 'Issue',
    })
      .pipe(switchMap(() => this.api.issue(i.id)))
      .subscribe(this.observer((issued) => `Issued ${issued.number}.`));
  }

  protected markPaid(i: Invoice): void {
    this.dialog
      .open<MarkPaidDialog, Invoice, Invoice>(MarkPaidDialog, { data: i })
      .afterClosed()
      .subscribe((paid) => {
        if (!paid) return;
        this.invoice.set(paid);
        this.snackBar.open(`${paid.number} marked paid.`, 'Close', { duration: 4000 });
      });
  }

  protected voidInvoice(i: Invoice): void {
    this.dialog
      .open<ReasonDialog, ReasonDialogData, string>(ReasonDialog, {
        data: { title: `Void ${i.number}`, label: 'Why is the invoice being voided?', confirm: 'Void invoice', danger: true },
      })
      .afterClosed()
      .pipe(
        filter((reason): reason is string => !!reason),
        switchMap((reason) => this.api.void(i.id, reason)),
      )
      .subscribe(this.observer(() => `Voided. ${i.workOrderNumber} is back to Completed and can be invoiced again.`));
  }

  protected remove(i: Invoice): void {
    this.confirm({ title: 'Delete this draft?', message: 'The job can be invoiced again afterwards.', confirmLabel: 'Delete draft' })
      .pipe(switchMap(() => this.api.delete(i.id)))
      .subscribe({
        next: () => {
          this.snackBar.open('Draft deleted.', 'Close', { duration: 4000 });
          void this.router.navigate(['/office/work-orders', i.workOrderId]);
        },
        error: (err: unknown) => this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 }),
      });
  }

  protected download(i: Invoice): void {
    this.busy.set(true);
    this.api.pdf(i.id).subscribe({
      next: (pdf) => {
        saveBlob(pdf.blob, pdf.fileName);
        this.busy.set(false);
      },
      error: (err: unknown) => {
        this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 });
        this.busy.set(false);
      },
    });
  }

  private run(request: Observable<Invoice>, done?: (i: Invoice) => string): void {
    request.subscribe(this.observer(done));
  }

  private observer(done?: (i: Invoice) => string) {
    this.busy.set(true);
    return {
      next: (updated: Invoice) => {
        this.invoice.set(updated);
        this.busy.set(false);
        if (done) this.snackBar.open(done(updated), 'Close', { duration: 5000 });
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 });
      },
      complete: () => this.busy.set(false),
    };
  }

  private confirm(data: ConfirmData): Observable<true> {
    return this.dialog
      .open<ConfirmDialog, ConfirmData, boolean>(ConfirmDialog, { data })
      .afterClosed()
      .pipe(filter((yes): yes is true => !!yes));
  }
}
