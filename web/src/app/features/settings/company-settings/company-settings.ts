import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { problemMessage } from '../../../core/problem';
import { SettingsApi } from './settings.api';

@Component({
  selector: 'app-company-settings',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Company settings</h1>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-form-field appearance="outline"><mat-label>Company name</mat-label><input matInput formControlName="companyName" /></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>Address</mat-label><textarea matInput formControlName="companyAddress"></textarea></mat-form-field>
      <mat-form-field appearance="outline"><mat-label>TRN (tax number)</mat-label><input matInput formControlName="trn" /></mat-form-field>
      <div class="row">
        <mat-form-field appearance="outline"><mat-label>VAT rate (%)</mat-label><input matInput type="number" formControlName="vatRate" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Labor rate per hour</mat-label><input matInput type="number" formControlName="laborRatePerHour" /></mat-form-field>
      </div>
      <div class="row">
        <mat-form-field appearance="outline"><mat-label>Invoice due days</mat-label><input matInput type="number" formControlName="invoiceDueDays" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Currency</mat-label><input matInput formControlName="currency" maxlength="3" /></mat-form-field>
      </div>
      <p class="hint">Changes apply only to invoices generated after you save.</p>
      @if (message()) {
        <p role="status">{{ message() }}</p>
      }
      <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">Save</button>
    </form>

    <section class="logo">
      <h2>Logo</h2>
      <p>{{ settings.value()?.hasLogo ? 'A logo is uploaded.' : 'No logo uploaded yet.' }}</p>
      <input #file type="file" accept="image/png,image/jpeg,image/webp" hidden (change)="upload(file.files?.[0])" />
      <button mat-stroked-button type="button" (click)="file.click()">Upload logo</button>
    </section>
  `,
  styles: `
    form { display: flex; flex-direction: column; max-width: 560px; }
    .row { display: flex; gap: 16px; flex-wrap: wrap; }
    .row mat-form-field { flex: 1; min-width: 200px; }
    .hint { color: var(--mat-sys-on-surface-variant); }
    .logo { margin-top: 32px; }
  `,
})
export class CompanySettingsPage {
  private readonly api = inject(SettingsApi);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly settings = rxResource({ stream: () => this.api.get() });

  protected readonly form = inject(NonNullableFormBuilder).group({
    companyName: ['', [Validators.required, Validators.maxLength(200)]],
    companyAddress: [''],
    trn: [''],
    vatRate: [5, [Validators.required, Validators.min(0), Validators.max(100)]],
    laborRatePerHour: [0, [Validators.required, Validators.min(0)]],
    invoiceDueDays: [30, [Validators.required, Validators.min(0)]],
    currency: ['AED', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
  });

  constructor() {
    effect(() => {
      const value = this.settings.value();
      if (value) this.form.reset({ ...value, companyAddress: value.companyAddress ?? '', trn: value.trn ?? '' });
    });
  }

  protected save(): void {
    const value = this.form.getRawValue();
    this.busy.set(true);
    this.api.update({ ...value, companyAddress: value.companyAddress || null, trn: value.trn || null }).subscribe({
      next: () => {
        this.message.set('Settings saved.');
        this.busy.set(false);
      },
      error: (err: unknown) => {
        this.message.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }

  protected upload(file: File | undefined): void {
    if (!file) return;
    this.api.uploadLogo(file).subscribe({
      next: () => this.settings.reload(),
      error: (err: unknown) => this.message.set(problemMessage(err)),
    });
  }
}
