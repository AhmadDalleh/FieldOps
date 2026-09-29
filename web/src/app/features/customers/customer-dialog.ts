import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../core/problem';
import { Customer, CustomerInput, CustomerType, CustomersApi } from './customers.api';

@Component({
  selector: 'app-customer-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ customer ? 'Edit customer' : 'New customer' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save(false)">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="name" /></mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Type</mat-label>
          <mat-select formControlName="type">
            <mat-option value="Business">Business</mat-option>
            <mat-option value="Individual">Individual</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Phone</mat-label><input matInput formControlName="phone" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Email</mat-label><input matInput type="email" formControlName="email" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>TRN (tax number)</mat-label><input matInput formControlName="taxRegistrationNumber" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Billing address</mat-label><textarea matInput formControlName="billingAddress"></textarea></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Notes</mat-label><textarea matInput formControlName="notes"></textarea></mat-form-field>
        @if (duplicate()) {
          <p class="warning" role="alert">A customer with this phone number already exists. Save anyway?</p>
        } @else if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        @if (duplicate()) {
          <button mat-flat-button type="button" [disabled]="busy()" (click)="save(true)">Save anyway</button>
        } @else {
          <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">Save</button>
        }
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(480px, 80vw); }
    .error { color: var(--mat-sys-error); }
    .warning { color: var(--mat-sys-tertiary); }
  `,
})
export class CustomerDialog {
  private readonly api = inject(CustomersApi);
  private readonly ref = inject(MatDialogRef<CustomerDialog, Customer>);
  protected readonly customer = inject<Customer | null>(MAT_DIALOG_DATA);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly duplicate = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.customer?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    type: [this.customer?.type ?? ('Business' as CustomerType), Validators.required],
    phone: [this.customer?.phone ?? '', [Validators.required, Validators.maxLength(30)]],
    email: [this.customer?.email ?? '', Validators.email],
    taxRegistrationNumber: [this.customer?.taxRegistrationNumber ?? ''],
    billingAddress: [this.customer?.billingAddress ?? ''],
    notes: [this.customer?.notes ?? ''],
  });

  constructor() {
    // Editing the phone after the warning asks the server again.
    this.form.controls.phone.valueChanges.subscribe(() => this.duplicate.set(false));
  }

  protected save(force: boolean): void {
    const v = this.form.getRawValue();
    const input: CustomerInput = {
      ...v,
      email: v.email || null,
      taxRegistrationNumber: v.taxRegistrationNumber || null,
      billingAddress: v.billingAddress || null,
      notes: v.notes || null,
    };
    const request = this.customer ? this.api.update(this.customer.id, input) : this.api.create(input, force);
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: (saved) => this.ref.close(saved),
      error: (err: unknown) => {
        this.busy.set(false);
        if (err instanceof HttpErrorResponse && err.error?.code === 'Customer.DuplicatePhone') {
          this.duplicate.set(true);
        } else {
          this.error.set(problemMessage(err));
        }
      },
    });
  }
}
