import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { problemMessage } from '../../core/problem';
import { InventoryApi, StockRow, unitLabel } from './inventory.api';
import { StockAction, StockDialog, StockDialogData, quantityAt } from './stock-dialog';

/** US-INV-02: every active part by location, with totals and low-stock highlighting. */
@Component({
  selector: 'app-stock',
  imports: [DecimalPipe, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, MatSlideToggleModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Stock by location</h1>
      <div class="actions">
        <a mat-stroked-button routerLink="/office/inventory/parts">Parts</a>
        <a mat-stroked-button routerLink="/office/inventory/movements">Movements</a>
      </div>
    </header>
    <div class="filters">
      <mat-form-field appearance="outline" class="search">
        <mat-label>Filter by name or SKU</mat-label>
        <input matInput (input)="search.set($any($event.target).value)" />
      </mat-form-field>
      <mat-slide-toggle [checked]="lowOnly()" (change)="lowOnly.set($event.checked)">Low stock only</mat-slide-toggle>
    </div>

    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    <div class="scroll">
      <table class="grid">
        <thead>
          <tr>
            <th>Part</th>
            @for (l of locations(); track l.id) {
              <th class="num">{{ l.name }}</th>
            }
            <th class="num">Total</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (r of rows(); track r.partId) {
            <tr [class.low]="r.isLow">
              <td>
                <strong>{{ r.sku }}</strong> {{ r.name }}
                @if (r.isLow) {
                  <span class="badge">Low · reorder at {{ r.reorderLevel | number: '1.0-2' }}</span>
                }
              </td>
              @for (l of locations(); track l.id) {
                <td class="num" [class.zero]="!at(r, l.id)">{{ at(r, l.id) | number: '1.0-2' }}</td>
              }
              <td class="num total">{{ r.total | number: '1.0-2' }} {{ unit(r.unit) }}</td>
              <td class="num row-actions">
                <button mat-button (click)="open('receive', r)">Receive</button>
                <button mat-button (click)="open('transfer', r)" [disabled]="!r.total">Transfer</button>
                @if (isAdmin()) {
                  <button mat-button (click)="open('adjust', r)">Adjust</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td class="muted" [attr.colspan]="locations().length + 3">{{ stock.isLoading() ? 'Loading…' : 'No parts to show.' }}</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: `
    .header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; }
    .actions { display: flex; gap: 8px; }
    .filters { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }
    .search { min-width: 320px; }
    .scroll { overflow-x: auto; }
    .grid { border-collapse: collapse; width: 100%; }
    .grid th, .grid td { padding: 8px 12px; border-bottom: 1px solid var(--mat-sys-outline-variant); text-align: left; white-space: nowrap; }
    .grid th { font: var(--mat-sys-title-small); color: var(--mat-sys-on-surface-variant); }
    .num { text-align: right !important; }
    .zero { color: var(--mat-sys-outline); }
    .total { font-weight: 600; }
    tr.low .total { color: var(--mat-sys-error); }
    .badge { display: inline-block; margin-left: 6px; padding: 1px 8px; border-radius: 10px; font: var(--mat-sys-label-small);
      background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class Stock {
  private readonly api = inject(InventoryApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  protected readonly unit = unitLabel;
  protected readonly at = quantityAt;

  protected readonly search = signal('');
  protected readonly lowOnly = signal(false);
  private readonly allLocations = rxResource({ stream: () => this.api.locations() });
  protected readonly locations = computed(() => (this.allLocations.value() ?? []).filter((l) => l.isActive));
  protected readonly stock = rxResource({ params: () => this.lowOnly(), stream: ({ params }) => this.api.stock(params) });
  protected readonly rows = computed(() => {
    const term = this.search().trim().toLowerCase();
    const rows = this.stock.value() ?? [];
    return term ? rows.filter((r) => r.sku.toLowerCase().includes(term) || r.name.toLowerCase().includes(term)) : rows;
  });
  protected readonly error = computed(() =>
    this.stock.error() ? problemMessage(this.stock.error()) : this.allLocations.error() ? problemMessage(this.allLocations.error()) : null,
  );

  protected open(action: StockAction, row: StockRow): void {
    this.dialog
      .open<StockDialog, StockDialogData, StockRow>(StockDialog, { data: { action, row, locations: this.locations() } })
      .afterClosed()
      .subscribe((saved) => {
        if (!saved) return;
        this.snackBar.open(`${saved.sku}: ${saved.total} ${unitLabel(saved.unit)} in total.`, 'Close', { duration: 4000 });
        this.stock.reload();
      });
  }
}
