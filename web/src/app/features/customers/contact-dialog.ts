import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { problemMessage } from '../../core/problem';
import { Contact, CustomersApi } from './customers.api';

export interface ContactDialogData {
  customerId: string;
  contact: Contact | null;
}

@Component({
  selector: 'app-contact-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatCheckboxModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.contact ? 'Edit contact' : 'New contact' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="name" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Job title</mat-label><input matInput formControlName="jobTitle" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Phone</mat-label><input matInput formControlName="phone" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Email</mat-label><input matInput type="email" formControlName="email" /></mat-form-field>
        <mat-checkbox formControlName="isPrimary">Primary contact</mat-checkbox>
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
export class ContactDialog {
  private readonly api = inject(CustomersApi);
  private readonly ref = inject(MatDialogRef<ContactDialog, boolean>);
  protected readonly data = inject<ContactDialogData>(MAT_DIALOG_DATA);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.contact?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    jobTitle: [this.data.contact?.jobTitle ?? ''],
    phone: [this.data.contact?.phone ?? ''],
    email: [this.data.contact?.email ?? '', Validators.email],
    isPrimary: [this.data.contact?.isPrimary ?? false],
  });

  protected save(): void {
    const v = this.form.getRawValue();
    const input = { ...v, jobTitle: v.jobTitle || null, phone: v.phone || null, email: v.email || null };
    const { customerId, contact } = this.data;
    const request = contact
      ? this.api.updateContact(customerId, contact.id, input)
      : this.api.addContact(customerId, input);
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
