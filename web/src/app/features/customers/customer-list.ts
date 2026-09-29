import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { CustomerDialog } from './customer-dialog';
import { Customer, CustomerQuery, CustomersApi } from './customers.api';

@Component({
  selector: 'app-customer-list',
  imports: [MatTableModule, MatPaginatorModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSlideToggleModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Customers</h1>
      <button mat-flat-button (click)="create()">New customer</button>
    </header>
    <div class="filters">
      <mat-form-field appearance="outline" class="search">
        <mat-label>Search by name, code, phone or email</mat-label>
        <input matInput (input)="search($any($event.target).value)" />
      </mat-form-field>
      <mat-slide-toggle (change)="showInactive($event.checked)">Show inactive</mat-slide-toggle>
    </div>

    <table mat-table [dataSource]="customers.value()?.items ?? []">
      <ng-container matColumnDef="code">
        <th mat-header-cell *matHeaderCellDef>Code</th>
        <td mat-cell *matCellDef="let c">{{ c.code }}</td>
      </ng-container>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>Name</th>
        <td mat-cell *matCellDef="let c"><a [routerLink]="[c.id]">{{ c.name }}</a></td>
      </ng-container>
      <ng-container matColumnDef="type">
        <th mat-header-cell *matHeaderCellDef>Type</th>
        <td mat-cell *matCellDef="let c">{{ c.type }}</td>
      </ng-container>
      <ng-container matColumnDef="phone">
        <th mat-header-cell *matHeaderCellDef>Phone</th>
        <td mat-cell *matCellDef="let c">{{ c.phone }}</td>
      </ng-container>
      <ng-container matColumnDef="email">
        <th mat-header-cell *matHeaderCellDef>Email</th>
        <td mat-cell *matCellDef="let c">{{ c.email }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>Status</th>
        <td mat-cell *matCellDef="let c">{{ c.isActive ? 'Active' : 'Inactive' }}</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    @if (customers.value()?.totalCount === 0) {
      <p class="empty">No customers found.</p>
    }
    <mat-paginator
      [length]="customers.value()?.totalCount ?? 0"
      [pageSize]="query().pageSize"
      [pageIndex]="query().page - 1"
      (page)="page($event)"
    />
  `,
  styles: `
    .header { display: flex; align-items: center; justify-content: space-between; }
    .filters { display: flex; align-items: center; gap: 24px; flex-wrap: wrap; }
    .search { width: 100%; max-width: 420px; }
    .empty { padding: 16px; color: var(--mat-sys-on-surface-variant); }
    a { color: var(--mat-sys-primary); }
  `,
})
export class CustomerList {
  private readonly api = inject(CustomersApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  protected readonly columns = ['code', 'name', 'type', 'phone', 'email', 'status'];
  protected readonly query = signal<CustomerQuery>({ page: 1, pageSize: 20, search: '', includeInactive: false });
  protected readonly customers = rxResource({ params: () => this.query(), stream: ({ params }) => this.api.list(params) });

  protected search(search: string): void {
    this.query.update((q) => ({ ...q, page: 1, search }));
  }

  protected showInactive(includeInactive: boolean): void {
    this.query.update((q) => ({ ...q, page: 1, includeInactive }));
  }

  protected page(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected create(): void {
    this.dialog
      .open(CustomerDialog, { data: null })
      .afterClosed()
      .subscribe((created: Customer | undefined) => {
        if (created) void this.router.navigate(['/office/customers', created.id]);
      });
  }
}
