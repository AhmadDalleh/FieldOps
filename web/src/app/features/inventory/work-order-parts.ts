import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';
import { problemMessage } from '../../core/problem';
import { VanStockItem, WorkOrderPart, WorkOrderPartsApi, isValidQuantity, unitLabel } from './inventory.api';

/** The sum of the lines, rounded to fils. */
export function partsTotal(lines: WorkOrderPart[]): number {
  return Math.round(lines.reduce((sum, l) => sum + l.lineTotal, 0) * 100) / 100;
}

/**
 * US-TAPP-07: the parts used on a job. With `canAdd` the technician takes parts from their van; removing a line
 * (while the job is open) puts the parts back where they came from.
 */
@Component({
  selector: 'app-work-order-parts',
  imports: [DecimalPipe, FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (l of lines(); track l.id) {
      <div class="line">
        <div>
          <strong>{{ l.quantity | number: '1.0-2' }} {{ unit(l.unit) }}</strong> {{ l.name }}
          <div class="muted small">{{ l.sku }} · from {{ l.locationName }} · {{ l.unitPrice | number: '1.2-2' }} AED each</div>
        </div>
        <div class="end">
          <span>{{ l.lineTotal | number: '1.2-2' }}</span>
          @if (l.canRemove) {
            <button mat-icon-button [attr.aria-label]="'Remove ' + l.name" [disabled]="busy()" (click)="remove(l)"><mat-icon>undo</mat-icon></button>
          }
        </div>
      </div>
    } @empty {
      <p class="muted">{{ loaded.isLoading() ? 'Loading…' : 'No parts used.' }}</p>
    }
    @if (lines().length) {
      <p class="total">Parts total: {{ total() | number: '1.2-2' }} AED</p>
    }

    @if (canAdd()) {
      <form class="add" (ngSubmit)="add()">
        <mat-form-field appearance="outline" class="part">
          <mat-label>Part from your van</mat-label>
          <mat-select [(ngModel)]="partId" name="partId">
            @for (s of available(); track s.partId) {
              <mat-option [value]="s.partId">{{ s.name }} ({{ s.quantity | number: '1.0-2' }} {{ unit(s.unit) }})</mat-option>
            }
          </mat-select>
          @if (van.hasValue() && available().length === 0) {
            <mat-hint>Your van is empty.</mat-hint>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" class="qty">
          <mat-label>Quantity</mat-label>
          <input matInput type="number" inputmode="decimal" min="0.01" step="0.01" [(ngModel)]="quantity" name="quantity" />
          @if (selected(); as s) {
            <mat-hint>{{ s.quantity | number: '1.0-2' }} {{ unit(s.unit) }} in the van</mat-hint>
          }
        </mat-form-field>
        <button mat-flat-button type="submit" [disabled]="!canSubmit() || busy()">Add part</button>
      </form>
    }
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 6px; width: 100%; }
    .line { display: flex; justify-content: space-between; align-items: center; gap: 8px; padding: 4px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .end { display: flex; align-items: center; gap: 4px; white-space: nowrap; }
    .total { margin: 4px 0; font-weight: 600; text-align: right; }
    .add { display: flex; flex-wrap: wrap; gap: 8px; align-items: baseline; margin-top: 8px; }
    .part { flex: 1 1 220px; }
    .qty { flex: 0 1 140px; }
    .add button { min-height: 48px; }
    .muted { color: var(--mat-sys-on-surface-variant); margin: 0; }
    .small { font: var(--mat-sys-body-small); }
    .error { color: var(--mat-sys-error); margin: 0; }
  `,
})
export class WorkOrderPartsPanel {
  private readonly api = inject(WorkOrderPartsApi);
  readonly workOrderId = input.required<string>();
  readonly canAdd = input(false);

  protected readonly loaded = rxResource({ params: () => this.workOrderId(), stream: ({ params }) => this.api.list(params) });
  protected readonly lines = linkedSignal<WorkOrderPart[]>(() => this.loaded.value() ?? []);
  protected readonly van = rxResource({
    params: () => (this.canAdd() ? true : undefined),
    stream: () => this.api.vanStock(),
  });
  protected readonly available = computed(() => (this.van.value() ?? []).filter((s) => s.quantity > 0));
  protected readonly partId = signal<string | null>(null);
  protected readonly quantity = signal<number | null>(1);
  protected readonly selected = computed<VanStockItem | undefined>(() => this.available().find((s) => s.partId === this.partId()));
  protected readonly canSubmit = computed(() => {
    const s = this.selected();
    const q = this.quantity();
    return !!s && isValidQuantity(q) && q! <= s.quantity;
  });
  protected readonly total = computed(() => partsTotal(this.lines()));
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly unit = unitLabel;

  protected add(): void {
    const partId = this.partId();
    const quantity = this.quantity();
    if (!partId || quantity == null) return;
    this.run(this.api.add(this.workOrderId(), partId, quantity), () => {
      this.partId.set(null);
      this.quantity.set(1);
    });
  }

  protected remove(line: WorkOrderPart): void {
    this.run(this.api.remove(this.workOrderId(), line.id));
  }

  private run(call: Observable<WorkOrderPart[]>, done?: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: (lines) => {
        this.lines.set(lines);
        if (this.canAdd()) this.van.reload();
        done?.();
        this.busy.set(false);
      },
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
        if (this.canAdd()) this.van.reload();
      },
    });
  }
}
