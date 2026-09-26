import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/auth.service';
import { problemMessage } from '../../../core/problem';
import { UserDialog } from './user-dialog';
import { User, UsersApi } from './users.api';

@Component({
  selector: 'app-users',
  imports: [MatTableModule, MatPaginatorModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Users</h1>
      <button mat-flat-button (click)="open(null)">New user</button>
    </header>
    <mat-form-field appearance="outline" class="search">
      <mat-label>Search by name or email</mat-label>
      <input matInput (input)="search($any($event.target).value)" />
    </mat-form-field>

    <table mat-table [dataSource]="users.value()?.items ?? []">
      <ng-container matColumnDef="fullName">
        <th mat-header-cell *matHeaderCellDef>Name</th>
        <td mat-cell *matCellDef="let u">{{ u.fullName }}</td>
      </ng-container>
      <ng-container matColumnDef="email">
        <th mat-header-cell *matHeaderCellDef>Email</th>
        <td mat-cell *matCellDef="let u">{{ u.email }}</td>
      </ng-container>
      <ng-container matColumnDef="role">
        <th mat-header-cell *matHeaderCellDef>Role</th>
        <td mat-cell *matCellDef="let u">{{ u.role }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>Status</th>
        <td mat-cell *matCellDef="let u">{{ u.isActive ? 'Active' : 'Inactive' }}</td>
      </ng-container>
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef></th>
        <td mat-cell *matCellDef="let u" class="actions">
          <button mat-button (click)="open(u)">Edit</button>
          @if (u.id !== auth.user()?.id) {
            <button mat-button (click)="toggleActive(u)">{{ u.isActive ? 'Deactivate' : 'Activate' }}</button>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    <mat-paginator [length]="users.value()?.totalCount ?? 0" [pageSize]="query().pageSize" [pageIndex]="query().page - 1" (page)="page($event)" />
  `,
  styles: `
    .header { display: flex; align-items: center; justify-content: space-between; }
    .search { width: 100%; max-width: 360px; }
    .actions { text-align: right; white-space: nowrap; }
  `,
})
export class Users {
  private readonly api = inject(UsersApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly auth = inject(AuthService);
  protected readonly columns = ['fullName', 'email', 'role', 'status', 'actions'];
  protected readonly query = signal({ page: 1, pageSize: 20, search: '' });
  protected readonly users = rxResource({
    params: () => this.query(),
    stream: ({ params }) => this.api.list(params.page, params.pageSize, params.search),
  });

  protected search(term: string): void {
    this.query.update((q) => ({ ...q, page: 1, search: term }));
  }

  protected page(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected open(user: User | null): void {
    this.dialog
      .open(UserDialog, { data: user })
      .afterClosed()
      .subscribe((saved) => saved && this.users.reload());
  }

  protected toggleActive(user: User): void {
    this.api.setActive(user.id, !user.isActive).subscribe({
      next: () => this.users.reload(),
      error: (err: unknown) => this.snackBar.open(problemMessage(err), 'Close', { duration: 5000 }),
    });
  }
}
