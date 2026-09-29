import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';
import { problemMessage } from '../../core/problem';
import { InventoryApi, StockLocation, StockRow, isValidQuantity, unitLabel } from './inventory.api';

export type StockAction = 'receive' | 'transfer' | 'adjust';

export interface StockDialogData {
  action: StockAction;
  row: StockRow;
  locations: StockLocation[];
}

export interface StockForm {
  fromLocationId: string;
  toLocationId: string;
  quantity: number | null;
  reason: string;
}

/** The quantity held at a location for a stock row. */
export function quantityAt(row: StockRow, locationId: string): number {
  return row.levels.find((l) => l.locationId === locationId)?.quantity ?? 0;
}

/** Why the form can't be sent yet, or null when it can. Mirrors the API's rules so the button explains itself. */
export function stockFormProblem(action: StockAction, row: StockRow, v: StockForm): string | null {
  if (action === 'adjust') {
    if (!v.toLocationId) return 'Choose a location.';
    if (v.quantity == null || v.quantity < 0 || !(v.quantity === 0 || isValidQuantity(v.quantity)))
      return 'Enter the counted quantity (zero or more, up to two decimals).';
    if (!v.reason.trim()) return 'Say why the stock is being adjusted.';
    return null;
  }
  if (action === 'transfer' && !v.fromLocationId) return 'Choose where the stock comes from.';
  if (!v.toLocationId) return 'Choose where the stock goes.';
  if (action === 'transfer' && v.fromLocationId === v.toLocationId) return 'Choose two different locations.';
  if (!isValidQuantity(v.quantity)) return 'Enter a quantity above zero with up to two decimals.';
  if (action === 'transfer' && v.quantity! > quantityAt(row, v.fromLocationId))
    return `Only ${quantityAt(row, v.fromLocationId)} ${unitLabel(row.unit)} there.`;
  return null;
}

const TITLES: Record<StockAction, string> = { receive: 'Receive', transfer: 'Transfer', adjust: 'Adjust' };

/** US-INV-03/04 and the admin count correction: one dialog for the three ways stock changes by hand. */
@Component({
  selector: 'app-stock-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ title }} {{ data.row.sku }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <p class="muted">{{ data.row.name }} · {{ data.row.total }} {{ unit }} in total</p>
        @if (data.action === 'transfer') {
          <mat-form-field appearance="outline">
            <mat-label>From</mat-label>
            <mat-select formControlName="fromLocationId">
              @for (l of sources(); track l.id) {
                <mat-option [value]="l.id">{{ l.name }} ({{ held(l.id) }} {{ unit }})</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <mat-form-field appearance="outline">
          <mat-label>{{ data.action === 'transfer' ? 'To' : 'Location' }}</mat-label>
          <mat-select formControlName="toLocationId">
            @for (l of targets(); track l.id) {
              <mat-option [value]="l.id">{{ l.name }} ({{ held(l.id) }} {{ unit }})</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ data.action === 'adjust' ? 'Counted quantity' : 'Quantity' }} ({{ unit }})</mat-label>
          <input matInput type="number" min="0" step="0.01" formControlName="quantity" />
        </mat-form-field>
        @if (data.action === 'adjust') {
          <mat-form-field appearance="outline">
            <mat-label>Reason</mat-label>
            <input matInput formControlName="reason" maxlength="500" />
          </mat-form-field>
        }
        @if (problem() && form.dirty) {
          <p class="hint">{{ problem() }}</p>
        }
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="!!problem() || busy()">{{ title }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(400px, 80vw); }
    .muted { color: var(--mat-sys-on-surface-variant); margin-top: 0; }
    .hint { color: var(--mat-sys-on-surface-variant); margin: 0; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class StockDialog {
  protected readonly data = inject<StockDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<StockDialog, StockRow>);
  private readonly api = inject(InventoryApi);
  protected readonly title = TITLES[this.data.action];
  protected readonly unit = unitLabel(this.data.row.unit);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly active = this.data.locations.filter((l) => l.isActive);
  private readonly warehouse = this.active.find((l) => l.type === 'Warehouse');
  protected readonly sources = signal(this.active.filter((l) => quantityAt(this.data.row, l.id) > 0));
  protected readonly targets = signal(
    this.data.action === 'receive' ? this.active.filter((l) => l.type === 'Warehouse') : this.active,
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    fromLocationId: [this.data.action === 'transfer' && this.warehouse && quantityAt(this.data.row, this.warehouse.id) > 0 ? this.warehouse.id : ''],
    toLocationId: [this.data.action === 'receive' ? (this.warehouse?.id ?? '') : ''],
    quantity: [null as number | null],
    reason: [''],
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly problem = computed(() => stockFormProblem(this.data.action, this.data.row, { ...this.form.getRawValue(), ...this.value() }));

  protected held(locationId: string): number {
    return quantityAt(this.data.row, locationId);
  }

  protected save(): void {
    const v = this.form.getRawValue();
    const qty = v.quantity ?? 0;
    const call: Observable<StockRow> =
      this.data.action === 'receive'
        ? this.api.receive(this.data.row.partId, v.toLocationId, qty)
        : this.data.action === 'transfer'
          ? this.api.transfer(this.data.row.partId, v.fromLocationId, v.toLocationId, qty)
          : this.api.adjust(this.data.row.partId, v.toLocationId, qty, v.reason.trim());
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: (row) => this.ref.close(row),
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
