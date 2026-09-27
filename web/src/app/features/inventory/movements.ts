import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { problemMessage } from '../../core/problem';
import { formatDubai } from '../../shared/time/dubai-time';
import { InventoryApi, MOVEMENT_TYPES, MovementQuery } from './inventory.api';

/** US-INV-06: the stock ledger, newest first. */
@Component({
  selector: 'app-movements',
  imports: [DecimalPipe, RouterLink, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Stock movements</h1>
      <div class="actions">
        <a mat-stroked-button routerLink="/office/inventory/parts">Parts</a>
        <a mat-stroked-button routerLink="/office/inventory/stock">Stock by location</a>
      </div>
    </header>
    <div class="filters">
      @if (query().partId) {
        <button mat-stroked-button (click)="clearPart()" aria-label="Show all parts">
          {{ partLabel() }}<mat-icon iconPositionEnd>close</mat-icon>
        </button>
      }
      <mat-form-field appearance="outline">
        <mat-label>Type</mat-label>
        <mat-select [value]="query().type" (selectionChange)="patch({ type: $event.value })">
          <mat-option [value]="null">Any</mat-option>
          @for (t of types; track t) {
            <mat-option [value]="t">{{ t }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Location</mat-label>
        <mat-select [value]="query().locationId" (selectionChange)="patch({ locationId: $event.value })">
          <mat-option [value]="null">Any</mat-option>
          @for (l of locations.value() ?? []; track l.id) {
            <mat-option [value]="l.id">{{ l.name }}</mat-option>
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
    </div>

    @if (movements.error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    <div class="scroll">
      <table mat-table [dataSource]="movements.value()?.items ?? []">
        <ng-container matColumnDef="when">
          <th mat-header-cell *matHeaderCellDef>When</th>
          <td mat-cell *matCellDef="let m">{{ when(m.createdAt) }}</td>
        </ng-container>
        <ng-container matColumnDef="type">
          <th mat-header-cell *matHeaderCellDef>Type</th>
          <td mat-cell *matCellDef="let m">{{ m.type }}</td>
        </ng-container>
        <ng-container matColumnDef="part">
          <th mat-header-cell *matHeaderCellDef>Part</th>
          <td mat-cell *matCellDef="let m"><strong>{{ m.sku }}</strong> {{ m.partName }}</td>
        </ng-container>
        <ng-container matColumnDef="qty">
          <th mat-header-cell *matHeaderCellDef class="num">Quantity</th>
          <td mat-cell *matCellDef="let m" class="num">{{ m.quantity | number: '1.0-2' }}</td>
        </ng-container>
        <ng-container matColumnDef="route">
          <th mat-header-cell *matHeaderCellDef>From / to</th>
          <td mat-cell *matCellDef="let m">{{ m.fromLocation ?? '—' }} → {{ m.toLocation ?? '—' }}</td>
        </ng-container>
        <ng-container matColumnDef="ref">
          <th mat-header-cell *matHeaderCellDef>Job or reason</th>
          <td mat-cell *matCellDef="let m">
            @if (m.workOrderId) {
              <a [routerLink]="['/office/work-orders', m.workOrderId]">{{ m.workOrderNumber }}</a>
            }
            {{ m.reason ?? '' }}
          </td>
        </ng-container>
        <ng-container matColumnDef="by">
          <th mat-header-cell *matHeaderCellDef>By</th>
          <td mat-cell *matCellDef="let m">{{ m.createdByName }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
        <tr class="mat-row" *matNoDataRow><td class="mat-cell muted" [attr.colspan]="columns.length">No movements match.</td></tr>
      </table>
    </div>
    <mat-paginator [length]="movements.value()?.totalCount ?? 0" [pageSize]="query().pageSize" [pageIndex]="query().page - 1"
      [pageSizeOptions]="[25, 50, 100]" (page)="page($event)" />
  `,
  styles: `
    .header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; }
    .actions { display: flex; gap: 8px; }
    .filters { display: flex; gap: 12px; align-items: baseline; flex-wrap: wrap; }
    .scroll { overflow-x: auto; }
    .num { text-align: right; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class Movements {
  private readonly api = inject(InventoryApi);
  private readonly router = inject(Router);
  protected readonly types = MOVEMENT_TYPES;
  protected readonly columns = ['when', 'type', 'part', 'qty', 'route', 'ref', 'by'];
  protected readonly when = formatDubai;

  protected readonly query = signal<MovementQuery>({
    page: 1,
    pageSize: 25,
    partId: inject(ActivatedRoute).snapshot.queryParamMap.get('partId'),
    type: null,
    locationId: null,
    from: null,
    to: null,
  });
  protected readonly locations = rxResource({ stream: () => this.api.locations() });
  protected readonly movements = rxResource({ params: () => this.query(), stream: ({ params }) => this.api.movements(params) });
  protected readonly error = computed(() => problemMessage(this.movements.error()));
  protected readonly partLabel = computed(() => {
    const first = this.movements.value()?.items[0];
    return first && first.partId === this.query().partId ? `${first.sku} only` : 'One part only';
  });

  protected patch(change: Partial<MovementQuery>): void {
    this.query.update((q) => ({ ...q, page: 1, ...change }));
  }

  protected page(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected clearPart(): void {
    this.patch({ partId: null });
    void this.router.navigate([], { queryParams: {} });
  }
}
