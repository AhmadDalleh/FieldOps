import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../core/problem';
import { Invoice, InvoiceLine, InvoiceLineInput, InvoicesApi, LINE_TYPES, lineAmount } from './invoices.api';

export interface LineDialogData {
  invoice: Invoice;
  line: InvoiceLine | null;
}

/** Why the line can't be saved yet, or null (the server's rules: a price below zero only on Other lines, for a discount). */
export function lineProblem(v: { lineType: string; description: string; quantity: number | null; unitPrice: number | null }): string | null {
  if (!v.description.trim()) return 'Enter a description.';
  if (v.quantity == null || v.quantity <= 0) return 'The quantity must be above zero.';
  if (v.unitPrice == null) return 'Enter a unit price.';
  if (v.unitPrice < 0 && v.lineType !== 'Other') return 'Only Other lines can be negative (for a discount).';
  const twoDecimals = (x: number) => Math.abs(Math.round(x * 100) - x * 100) < 1e-6;
  if (!twoDecimals(v.quantity) || !twoDecimals(v.unitPrice)) return 'Use at most two decimals.';
  return null;
}

/** US-BIL-02: add or edit a line on a draft invoice. */
@Component({
  selector: 'app-line-dialog',
  imports: [DecimalPipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.line ? 'Edit line' : 'Add line' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline">
          <mat-label>Type</mat-label>
          <mat-select formControlName="lineType">
            @for (t of types; track t) {
              <mat-option [value]="t">{{ t }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Description</mat-label>
          <input matInput formControlName="description" maxlength="300" />
        </mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Quantity</mat-label>
            <input matInput type="number" step="0.01" formControlName="quantity" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Unit price ({{ data.invoice.currency }})</mat-label>
            <input matInput type="number" step="0.01" formControlName="unitPrice" />
            @if (value().lineType === 'Other') {
              <mat-hint>Negative for a discount</mat-hint>
            }
          </mat-form-field>
        </div>
        <p class="amount">Line amount: {{ amount() | number: '1.2-2' }} {{ data.invoice.currency }}</p>
        @if (problem() && form.dirty) {
          <p class="hint">{{ problem() }}</p>
        }
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="!!problem() || busy()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(420px, 80vw); }
    .row { display: flex; gap: 12px; }
    .row mat-form-field { flex: 1; }
    .amount { margin: 8px 0 0; font-weight: 600; }
    .hint { color: var(--mat-sys-on-surface-variant); margin: 4px 0 0; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class LineDialog {
  protected readonly data = inject<LineDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<LineDialog, Invoice>);
  private readonly api = inject(InvoicesApi);
  protected readonly types = LINE_TYPES;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    lineType: [this.data.line?.lineType ?? 'Other', Validators.required],
    description: [this.data.line?.description ?? ''],
    quantity: [(this.data.line?.quantity ?? 1) as number | null],
    unitPrice: [(this.data.line?.unitPrice ?? null) as number | null],
  });
  protected readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly problem = computed(() => lineProblem({ ...this.form.getRawValue(), ...this.value() }));
  protected readonly amount = computed(() => lineAmount(this.value().quantity ?? null, this.value().unitPrice ?? null));

  protected save(): void {
    const v = this.form.getRawValue();
    const input: InvoiceLineInput = { lineType: v.lineType, description: v.description.trim(), quantity: v.quantity!, unitPrice: v.unitPrice! };
    const id = this.data.invoice.id;
    this.busy.set(true);
    this.error.set(null);
    (this.data.line ? this.api.updateLine(id, this.data.line.id, input) : this.api.addLine(id, input)).subscribe({
      next: (invoice) => this.ref.close(invoice),
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
