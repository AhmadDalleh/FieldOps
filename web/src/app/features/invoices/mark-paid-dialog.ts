import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { problemMessage } from '../../core/problem';
import { dubaiToday } from '../../shared/time/dubai-time';
import { Invoice, InvoicesApi } from './invoices.api';

/** US-BIL-05: record when and how an issued invoice was paid. */
@Component({
  selector: 'app-mark-paid-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Mark {{ invoice.number }} paid</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline">
          <mat-label>Paid on</mat-label>
          <input matInput type="date" formControlName="paidAt" [min]="invoice.issueDate" [max]="today" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Payment reference</mat-label>
          <input matInput formControlName="reference" maxlength="100" placeholder="Transfer or cheque number" />
        </mat-form-field>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">Mark paid</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(360px, 80vw); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class MarkPaidDialog {
  protected readonly invoice = inject<Invoice>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<MarkPaidDialog, Invoice>);
  private readonly api = inject(InvoicesApi);
  protected readonly today = dubaiToday();
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    paidAt: [this.today, Validators.required],
    reference: ['', [Validators.required, Validators.pattern(/\S/)]],
  });

  protected save(): void {
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.api.markPaid(this.invoice.id, v.paidAt, v.reference.trim()).subscribe({
      next: (invoice) => this.ref.close(invoice),
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
