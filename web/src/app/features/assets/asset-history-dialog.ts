import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import { DubaiTimePipe } from '../../shared/time/dubai-time.pipe';
import { AuthService } from '../../core/auth.service';
import { AssetsApi } from './assets.api';

@Component({
  selector: 'app-asset-history-dialog',
  imports: [MatDialogModule, MatButtonModule, RouterLink, DubaiTimePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Service history: {{ asset.name }}</h2>
    <mat-dialog-content>
      @for (item of history.value() ?? []; track item.workOrderId) {
        <div class="item">
          <div>
            @if (isOffice) {
              <a [routerLink]="['/office/work-orders', item.workOrderId]" mat-dialog-close>{{ item.workOrderNumber }}</a>
            } @else {
              <strong>{{ item.workOrderNumber }}</strong>
            }
            · {{ item.date | dubaiTime }} · {{ item.type }} · {{ item.status }}
          </div>
          <div class="muted">{{ item.technicianName || 'Unassigned' }}</div>
          @if (item.completionNotes) {
            <div class="notes">{{ item.completionNotes }}</div>
          }
        </div>
      } @empty {
        @if (history.hasValue()) {
          <p>No service history yet.</p>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-button mat-dialog-close>Close</button></mat-dialog-actions>
  `,
  styles: `
    .item { padding: 8px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); min-width: min(480px, 80vw); }
    .muted { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .notes { white-space: pre-wrap; margin-top: 4px; }
  `,
})
export class AssetHistoryDialog {
  private readonly api = inject(AssetsApi);
  protected readonly asset = inject<{ id: string; name: string }>(MAT_DIALOG_DATA);
  protected readonly history = rxResource({ stream: () => this.api.history(this.asset.id) });
  protected readonly isOffice = inject(AuthService).user()?.role !== 'Technician';
}
