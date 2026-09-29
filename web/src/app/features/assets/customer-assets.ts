import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { WarrantyBadge } from '../../shared/ui/warranty-badge/warranty-badge';
import { Site } from '../customers/customers.api';
import { AssetDialog, AssetDialogData } from './asset-dialog';
import { AssetHistoryDialog } from './asset-history-dialog';
import { Asset, AssetsApi } from './assets.api';

/** The Assets tab of the customer page: every asset across the customer's sites. */
@Component({
  selector: 'app-customer-assets',
  imports: [MatTableModule, MatButtonModule, WarrantyBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tab-actions">
      <button mat-flat-button [disabled]="activeSites().length === 0" (click)="open(null)">Add asset</button>
    </div>
    @if (activeSites().length === 0) {
      <p class="empty">Add an active site first, then register its equipment.</p>
    }
    <table mat-table [dataSource]="assets.value() ?? []">
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>Asset</th>
        <td mat-cell *matCellDef="let a">
          <div>{{ a.name }}</div>
          <app-warranty-badge [expiresOn]="a.warrantyExpiresOn" [underWarranty]="a.underWarranty" />
        </td>
      </ng-container>
      <ng-container matColumnDef="site">
        <th mat-header-cell *matHeaderCellDef>Site</th>
        <td mat-cell *matCellDef="let a">{{ a.siteName }}</td>
      </ng-container>
      <ng-container matColumnDef="type">
        <th mat-header-cell *matHeaderCellDef>Type</th>
        <td mat-cell *matCellDef="let a">{{ a.assetType }}</td>
      </ng-container>
      <ng-container matColumnDef="make">
        <th mat-header-cell *matHeaderCellDef>Make / model</th>
        <td mat-cell *matCellDef="let a">{{ a.manufacturer }} {{ a.model }}</td>
      </ng-container>
      <ng-container matColumnDef="serial">
        <th mat-header-cell *matHeaderCellDef>Serial</th>
        <td mat-cell *matCellDef="let a">{{ a.serialNumber }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>Status</th>
        <td mat-cell *matCellDef="let a">{{ a.status === 'OutOfService' ? 'Out of service' : a.status }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let a" class="row-actions">
          <button mat-button (click)="showHistory(a)">History</button>
          <button mat-button (click)="open(a)">Edit</button>
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if (assets.value()?.length === 0 && activeSites().length > 0) {
      <p class="empty">No assets yet.</p>
    }
  `,
  styles: `
    .tab-actions { display: flex; justify-content: flex-end; padding: 16px 0 8px; }
    .row-actions { text-align: right; white-space: nowrap; }
    .empty { padding: 16px 0; color: var(--mat-sys-on-surface-variant); }
    td div { margin-bottom: 4px; }
  `,
})
export class CustomerAssets {
  private readonly api = inject(AssetsApi);
  private readonly dialog = inject(MatDialog);

  readonly customerId = input.required<string>();
  readonly sites = input<Site[]>([]);

  protected readonly columns = ['name', 'site', 'type', 'make', 'serial', 'status', 'actions'];
  protected readonly activeSites = computed(() => this.sites().filter((s) => s.isActive));
  protected readonly assets = rxResource({
    params: () => this.customerId(),
    stream: ({ params }) => this.api.forCustomer(params),
  });

  protected open(asset: Asset | null): void {
    const data: AssetDialogData = { sites: this.activeSites().map((s) => ({ id: s.id, name: s.name })), asset };
    this.dialog
      .open(AssetDialog, { data, maxWidth: '95vw' })
      .afterClosed()
      .subscribe((saved) => saved && this.assets.reload());
  }

  protected showHistory(asset: Asset): void {
    this.dialog.open(AssetHistoryDialog, { data: asset, width: '480px', maxWidth: '95vw' });
  }
}
