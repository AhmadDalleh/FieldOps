import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { AuthService, homeUrlFor } from '../../../core/auth.service';
import { problemMessage } from '../../../core/problem';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="login">
      <mat-card appearance="outlined">
        <mat-card-header><mat-card-title>FieldOps</mat-card-title></mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="username" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Password</mat-label>
              <input matInput type="password" formControlName="password" autocomplete="current-password" />
            </mat-form-field>
            @if (error()) {
              <p class="error" role="alert">{{ error() }}</p>
            }
            <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">Log in</button>
          </form>
        </mat-card-content>
      </mat-card>
    </main>
  `,
  styles: `
    .login { min-height: 100vh; display: grid; place-items: center; padding: 16px; box-sizing: border-box; }
    mat-card { width: 100%; max-width: 380px; }
    form { display: flex; flex-direction: column; gap: 4px; margin-top: 16px; }
    .error { color: var(--mat-sys-error); margin: 0 0 12px; }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit(): void {
    if (this.form.invalid) return;
    const { email, password } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(email, password).subscribe({
      next: (user) => void this.router.navigateByUrl(homeUrlFor(user.role)),
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
