import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../../core/problem';
import { Role } from '../../../shared/models/auth';
import { PASSWORD_PATTERN } from '../../account/change-password/change-password';
import { User, UsersApi } from './users.api';

@Component({
  selector: 'app-user-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ user ? 'Edit user' : 'New user' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline"><mat-label>Full name</mat-label><input matInput formControlName="fullName" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Email</mat-label><input matInput type="email" formControlName="email" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Phone</mat-label><input matInput formControlName="phoneNumber" /></mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">
            @for (role of roles; track role) {
              <mat-option [value]="role">{{ role }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (!user) {
          <mat-form-field appearance="outline">
            <mat-label>Temporary password</mat-label>
            <input matInput formControlName="temporaryPassword" />
            <mat-hint>At least 8 characters, with a digit and an uppercase letter.</mat-hint>
          </mat-form-field>
        }
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(420px, 80vw); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class UserDialog {
  private readonly api = inject(UsersApi);
  private readonly ref = inject(MatDialogRef<UserDialog, boolean>);
  protected readonly user = inject<User | null>(MAT_DIALOG_DATA);
  protected readonly roles: Role[] = ['Admin', 'Dispatcher', 'Technician'];
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: [this.user?.fullName ?? '', [Validators.required, Validators.maxLength(200)]],
    email: [this.user?.email ?? '', [Validators.required, Validators.email]],
    phoneNumber: [this.user?.phoneNumber ?? ''],
    role: [this.user?.role ?? ('Technician' as Role), Validators.required],
    temporaryPassword: ['', this.user ? [] : [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
  });

  protected save(): void {
    const { temporaryPassword, ...value } = this.form.getRawValue();
    const input = { ...value, phoneNumber: value.phoneNumber || null };
    const request = this.user ? this.api.update(this.user.id, input) : this.api.create({ ...input, temporaryPassword });
    this.busy.set(true);
    request.subscribe({
      next: () => this.ref.close(true),
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
