import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { problemMessage } from '../../core/problem';
import { saveBlob } from '../../shared/files/download';
import { dubaiToday } from '../../shared/time/dubai-time';
import { ReportFilter, ReportKind, ReportsApi, RevenueGrouping, formatMinutes, monthStart, periodProblem } from './reports.api';

/** US-RPT-01..03: the admin's reports for a date range, each downloadable as CSV. */
@Component({
  selector: 'app-reports',
  imports: [DecimalPipe, MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTableModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Reports</h1>
    <div class="controls">
      <mat-button-toggle-group [value]="kind()" (change)="kind.set($event.value)" aria-label="Report">
        <mat-button-toggle value="technicians">Technicians</mat-button-toggle>
        <mat-button-toggle value="revenue">Revenue</mat-button-toggle>
        <mat-button-toggle value="parts-usage">Parts usage</mat-button-toggle>
      </mat-button-toggle-group>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>From</mat-label>
        <input matInput type="date" [value]="from()" (change)="from.set($any($event.target).value)" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>To</mat-label>
        <input matInput type="date" [value]="to()" (change)="to.set($any($event.target).value)" />
      </mat-form-field>
      @if (kind() === 'revenue') {
        <mat-button-toggle-group [value]="groupBy()" (change)="groupBy.set($event.value)" aria-label="Group by">
          <mat-button-toggle value="Month">By month</mat-button-toggle>
          <mat-button-toggle value="Customer">By customer</mat-button-toggle>
        </mat-button-toggle-group>
      }
      <span class="spacer"></span>
      <button mat-stroked-button (click)="download()" [disabled]="!!problem() || downloading()">
        <mat-icon>download</mat-icon>Download CSV
      </button>
    </div>

    @if (problem(); as p) {
      <p class="error" role="alert">{{ p }}</p>
    } @else if (error()) {
      <p class="error" role="alert">Could not load the report.</p>
    }

    @switch (kind()) {
      @case ('technicians') {
        @if (technicians.value(); as r) {
          <table mat-table [dataSource]="r.rows" aria-label="Jobs per technician">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Technician</th>
              <td mat-cell *matCellDef="let row">{{ row.name }} <small>{{ row.employeeCode }}</small></td>
              <td mat-footer-cell *matFooterCellDef>Total</td>
            </ng-container>
            <ng-container matColumnDef="jobs">
              <th mat-header-cell *matHeaderCellDef class="num">Completed jobs</th>
              <td mat-cell *matCellDef="let row" class="num">{{ row.completedJobs }}</td>
              <td mat-footer-cell *matFooterCellDef class="num">{{ r.completedJobs }}</td>
            </ng-container>
            <ng-container matColumnDef="average">
              <th mat-header-cell *matHeaderCellDef class="num">Average duration</th>
              <td mat-cell *matCellDef="let row" class="num">{{ minutes(row.averageMinutes) }}</td>
              <td mat-footer-cell *matFooterCellDef class="num">{{ minutes(r.averageMinutes) }}</td>
            </ng-container>
            <ng-container matColumnDef="hours">
              <th mat-header-cell *matHeaderCellDef class="num">Work hours</th>
              <td mat-cell *matCellDef="let row" class="num">{{ row.workHours | number: '1.2-2' }}</td>
              <td mat-footer-cell *matFooterCellDef class="num">{{ r.workHours | number: '1.2-2' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="technicianColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: technicianColumns"></tr>
            <tr mat-footer-row *matFooterRowDef="technicianColumns"></tr>
          </table>
          <p class="hint">Jobs completed in the period. Duration runs from the first start to completion; work hours are logged work time.</p>
        }
      }
      @case ('revenue') {
        @if (revenue.value(); as r) {
          <table mat-table [dataSource]="r.rows" aria-label="Revenue">
            <ng-container matColumnDef="label">
              <th mat-header-cell *matHeaderCellDef>{{ r.groupBy === 'Month' ? 'Month' : 'Customer' }}</th>
              <td mat-cell *matCellDef="let row">{{ row.label }}</td>
              <td mat-footer-cell *matFooterCellDef>Total</td>
            </ng-container>
            <ng-container matColumnDef="invoices">
              <th mat-header-cell *matHeaderCellDef class="num">Invoices</th>
              <td mat-cell *matCellDef="let row" class="num">{{ row.invoices }}</td>
              <td mat-footer-cell *matFooterCellDef class="num">{{ r.totals.invoices }}</td>
            </ng-container>
            @for (c of moneyColumns; track c.key) {
              <ng-container [matColumnDef]="c.key">
                <th mat-header-cell *matHeaderCellDef class="num">{{ c.label }}</th>
                <td mat-cell *matCellDef="let row" class="num">{{ row[c.key] | number: '1.2-2' }}</td>
                <td mat-footer-cell *matFooterCellDef class="num">{{ $any(r.totals)[c.key] | number: '1.2-2' }}</td>
              </ng-container>
            }
            <tr mat-header-row *matHeaderRowDef="revenueColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: revenueColumns"></tr>
            <tr mat-footer-row *matFooterRowDef="revenueColumns"></tr>
          </table>
          @if (!r.rows.length) {
            <p class="empty">No invoices were issued in this period.</p>
          }
          <p class="hint">Issued and paid invoices by issue date, in AED. Drafts and voided invoices are left out.</p>
        }
      }
      @case ('parts-usage') {
        @if (partsUsage.value(); as r) {
          <table mat-table [dataSource]="r.rows" aria-label="Parts usage">
            <ng-container matColumnDef="part">
              <th mat-header-cell *matHeaderCellDef>Part</th>
              <td mat-cell *matCellDef="let row">{{ row.name }} <small>{{ row.sku }}</small></td>
              <td mat-footer-cell *matFooterCellDef>Total</td>
            </ng-container>
            <ng-container matColumnDef="quantity">
              <th mat-header-cell *matHeaderCellDef class="num">Quantity</th>
              <td mat-cell *matCellDef="let row" class="num">{{ row.quantity | number: '1.0-2' }} {{ row.unit }}</td>
              <td mat-footer-cell *matFooterCellDef></td>
            </ng-container>
            <ng-container matColumnDef="jobs">
              <th mat-header-cell *matHeaderCellDef class="num">Jobs</th>
              <td mat-cell *matCellDef="let row" class="num">{{ row.jobs }}</td>
              <td mat-footer-cell *matFooterCellDef></td>
            </ng-container>
            <ng-container matColumnDef="cost">
              <th mat-header-cell *matHeaderCellDef class="num">Cost</th>
              <td mat-cell *matCellDef="let row" class="num">{{ row.cost | number: '1.2-2' }}</td>
              <td mat-footer-cell *matFooterCellDef class="num">{{ r.cost | number: '1.2-2' }}</td>
            </ng-container>
            <ng-container matColumnDef="price">
              <th mat-header-cell *matHeaderCellDef class="num">Price</th>
              <td mat-cell *matCellDef="let row" class="num">{{ row.price | number: '1.2-2' }}</td>
              <td mat-footer-cell *matFooterCellDef class="num">{{ r.price | number: '1.2-2' }}</td>
            </ng-container>
            <ng-container matColumnDef="margin">
              <th mat-header-cell *matHeaderCellDef class="num">Margin</th>
              <td mat-cell *matCellDef="let row" class="num" [class.loss]="row.margin < 0">
                {{ row.margin | number: '1.2-2' }}
                @if (row.marginPercent !== null) {
                  <small>({{ row.marginPercent | number: '1.1-1' }}%)</small>
                }
              </td>
              <td mat-footer-cell *matFooterCellDef class="num" [class.loss]="r.margin < 0">
                {{ r.margin | number: '1.2-2' }}
                @if (r.marginPercent !== null) {
                  <small>({{ r.marginPercent | number: '1.1-1' }}%)</small>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="partsColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: partsColumns"></tr>
            <tr mat-footer-row *matFooterRowDef="partsColumns"></tr>
          </table>
          @if (!r.rows.length) {
            <p class="empty">No parts were used in this period.</p>
          }
          <p class="hint">Price is what was charged when the part was used; cost uses each part's current unit cost.</p>
        }
      }
    }
  `,
  styles: `
    .controls { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 16px; margin-bottom: 16px; }
    .spacer { flex: 1; }
    table { width: 100%; }
    .num { text-align: right; }
    small { color: var(--mat-sys-on-surface-variant); margin-left: 4px; }
    td[mat-footer-cell] { font-weight: 600; }
    .loss { color: var(--mat-sys-error); }
    .hint, .empty { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class Reports {
  private readonly api = inject(ReportsApi);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly kind = signal<ReportKind>('technicians');
  protected readonly to = signal(dubaiToday());
  protected readonly from = signal(monthStart(dubaiToday()));
  protected readonly groupBy = signal<RevenueGrouping>('Month');
  protected readonly downloading = signal(false);
  protected readonly problem = computed(() => periodProblem(this.from(), this.to()));

  private readonly filter = computed<ReportFilter | undefined>(() =>
    this.problem() ? undefined : { from: this.from(), to: this.to(), groupBy: this.groupBy() },
  );
  /** Only the visible report loads. */
  private params(kind: ReportKind) {
    return () => (this.kind() === kind ? this.filter() : undefined);
  }

  protected readonly technicians = rxResource({ params: this.params('technicians'), stream: ({ params }) => this.api.technicians(params) });
  protected readonly revenue = rxResource({ params: this.params('revenue'), stream: ({ params }) => this.api.revenue(params) });
  protected readonly partsUsage = rxResource({ params: this.params('parts-usage'), stream: ({ params }) => this.api.partsUsage(params) });
  protected readonly error = computed(() => this.technicians.error() ?? this.revenue.error() ?? this.partsUsage.error());

  protected readonly technicianColumns = ['name', 'jobs', 'average', 'hours'];
  protected readonly moneyColumns = [
    { key: 'subtotal', label: 'Subtotal' },
    { key: 'vat', label: 'VAT' },
    { key: 'total', label: 'Total' },
    { key: 'paid', label: 'Paid' },
    { key: 'outstanding', label: 'Outstanding' },
  ] as const;
  protected readonly revenueColumns = ['label', 'invoices', ...this.moneyColumns.map((c) => c.key)];
  protected readonly partsColumns = ['part', 'quantity', 'jobs', 'cost', 'price', 'margin'];
  protected readonly minutes = formatMinutes;

  protected download(): void {
    const filter = this.filter();
    if (!filter) return;
    this.downloading.set(true);
    this.api.csv(this.kind(), filter).subscribe({
      next: (file) => {
        saveBlob(file.blob, file.fileName);
        this.downloading.set(false);
      },
      error: (err: unknown) => {
        this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 });
        this.downloading.set(false);
      },
    });
  }
}
