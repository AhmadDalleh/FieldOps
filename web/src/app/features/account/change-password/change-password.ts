import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { problemMessage } from '../../../core/problem';

/** Mirrors the API password policy: 8+ characters, a digit and an uppercase letter. */
export const PASSWORD_PATTERN = /^(?=.*\d)(?=.*[A-Z]).{8,}$/;

@Component({
  selector: 'app-change-password',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Change password</h1>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-form-field appearance="outline">
        <mat-label>Current password</mat-label>
        <input matInput type="password" formControlName="currentPassword" autocomplete="current-password" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>New password</mat-label>
        <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
        <mat-hint>At least 8 characters, with a digit and an uppercase letter.</mat-hint>
      </mat-form-field>
      @if (message()) {
        <p role="status">{{ message() }}</p>
      }
      <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">Change password</button>
    </form>
  `,
  styles: `form { display: flex; flex-direction: column; gap: 8px; max-width: 400px; }`,
})
export class ChangePassword {
  private readonly http = inject(HttpClient);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
  });

  protected submit(): void {
    this.busy.set(true);
    this.http.post('/api/auth/change-password', this.form.getRawValue()).subscribe({
      next: () => {
        this.message.set('Your password was changed.');
        this.form.reset();
        this.busy.set(false);
      },
      error: (err: unknown) => {
        this.message.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
