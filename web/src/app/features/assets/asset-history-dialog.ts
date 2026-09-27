import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { Asset, AssetsApi } from './assets.api';

@Component({
  selector: 'app-asset-history-dialog',
  imports: [MatDialogModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Service history: {{ asset.name }}</h2>
    <mat-dialog-content>
      @for (item of history.value() ?? []; track item.workOrderId) {
        <p><strong>{{ item.workOrderNumber }}</strong> · {{ item.type }} · {{ item.status }}</p>
      } @empty {
        <p>No service history yet.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-button mat-dialog-close>Close</button></mat-dialog-actions>
  `,
})
export class AssetHistoryDialog {
  private readonly api = inject(AssetsApi);
  protected readonly asset = inject<Asset>(MAT_DIALOG_DATA);
  protected readonly history = rxResource({ stream: () => this.api.history(this.asset.id) });
}
