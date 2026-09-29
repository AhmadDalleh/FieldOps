import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { InventoryApi, Part, PartQuery, unitLabel } from './inventory.api';
import { PartDialog } from './part-dialog';

/** US-INV-01/02: the parts catalog with total stock and low-stock flags. */
@Component({
  selector: 'app-parts',
  imports: [DecimalPipe, RouterLink, MatTableModule, MatPaginatorModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSlideToggleModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Parts</h1>
      <div class="actions">
        <a mat-stroked-button routerLink="/office/inventory/stock">Stock by location</a>
        @if (isAdmin()) {
          <button mat-flat-button (click)="edit(null)">New part</button>
        }
      </div>
    </header>
    <div class="filters">
      <mat-form-field appearance="outline" class="search">
        <mat-label>Search by name or SKU</mat-label>
        <input matInput (input)="patch({ search: $any($event.target).value, page: 1 })" />
      </mat-form-field>
      <mat-slide-toggle (change)="patch({ lowOnly: $event.checked, page: 1 })">Low stock only</mat-slide-toggle>
      <mat-slide-toggle (change)="patch({ includeInactive: $event.checked, page: 1 })">Show inactive</mat-slide-toggle>
    </div>

    <table mat-table [dataSource]="parts.value()?.items ?? []">
      <ng-container matColumnDef="sku">
        <th mat-header-cell *matHeaderCellDef>SKU</th>
        <td mat-cell *matCellDef="let p">{{ p.sku }}</td>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>Name</th>
        <td mat-cell *matCellDef="let p">{{ p.name }}@if (!p.isActive) {<span class="muted"> · inactive</span>}</td>
      </ng-container>
      <ng-container matColumnDef="price">
        <th mat-header-cell *matHeaderCellDef class="num">Cost / price (AED)</th>
        <td mat-cell *matCellDef="let p" class="num">{{ p.unitCost | number: '1.2-2' }} / {{ p.unitPrice | number: '1.2-2' }}</td>
      </ng-container>
      <ng-container matColumnDef="stock">
        <th mat-header-cell *matHeaderCellDef class="num">In stock</th>
        <td mat-cell *matCellDef="let p" class="num" [class.low]="p.isLow">
          {{ p.totalQuantity | number: '1.0-2' }} {{ unit(p.unit) }}
          @if (p.isLow) {
            <span class="badge">Low · reorder at {{ p.reorderLevel | number: '1.0-2' }}</span>
          }
        </td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let p" class="num">
          <a mat-button routerLink="/office/inventory/movements" [queryParams]="{ partId: p.id }">History</a>
          @if (isAdmin()) {
            <button mat-button (click)="edit(p)">Edit</button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
      <tr class="mat-row" *matNoDataRow><td class="mat-cell muted" [attr.colspan]="columns.length">No parts found.</td></tr>
    </table>
    <mat-paginator [length]="parts.value()?.totalCount ?? 0" [pageSize]="query().pageSize" [pageIndex]="query().page - 1"
      [pageSizeOptions]="[20, 50, 100]" (page)="page($event)" />
  `,
  styles: `
    .header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; }
    .actions { display: flex; gap: 8px; }
    .filters { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }
    .search { min-width: 320px; }
    .num { text-align: right; }
    .low { color: var(--mat-sys-error); font-weight: 600; }
    .badge { display: inline-block; margin-left: 6px; padding: 1px 8px; border-radius: 10px; font: var(--mat-sys-label-small);
      background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
    .muted { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class Parts {
  private readonly api = inject(InventoryApi);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  protected readonly columns = ['sku', 'name', 'price', 'stock', 'actions'];
  protected readonly unit = unitLabel;
  protected readonly query = signal<PartQuery>({ page: 1, pageSize: 20, search: '', includeInactive: false, lowOnly: false });
  protected readonly parts = rxResource({ params: () => this.query(), stream: ({ params }) => this.api.parts(params) });

  protected patch(change: Partial<PartQuery>): void {
    this.query.update((q) => ({ ...q, ...change }));
  }

  protected page(event: PageEvent): void {
    this.patch({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected edit(part: Part | null): void {
    this.dialog
      .open(PartDialog, { data: part })
      .afterClosed()
      .subscribe((saved) => saved && this.parts.reload());
  }
}
