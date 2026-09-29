import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { problemMessage } from '../../core/problem';
import { LatLng, MapPicker } from '../../shared/ui/map-picker/map-picker';
import { CustomersApi, Site } from './customers.api';

export interface SiteDialogData {
  customerId: string;
  site: Site | null;
}

@Component({
  selector: 'app-site-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MapPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.site ? 'Edit site' : 'New site' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline"><mat-label>Site name</mat-label><input matInput formControlName="name" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Address line 1</mat-label><input matInput formControlName="addressLine1" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Address line 2</mat-label><input matInput formControlName="addressLine2" /></mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline"><mat-label>City</mat-label><input matInput formControlName="city" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Emirate / region</mat-label><input matInput formControlName="region" /></mat-form-field>
        </div>
        <p class="label">Location: {{ location() ? location()!.lat + ', ' + location()!.lng : 'click the map to drop a pin' }}
          @if (location()) {
            <button mat-button type="button" (click)="location.set(null)">Clear pin</button>
          }
        </p>
        <app-map-picker [(value)]="location" />
        <mat-form-field appearance="outline" class="notes">
          <mat-label>Access notes</mat-label>
          <textarea matInput formControlName="accessNotes" placeholder="Gate code, parking, contact on site"></textarea>
        </mat-form-field>
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
    .fields { display: flex; flex-direction: column; min-width: min(560px, 80vw); }
    .row { display: flex; gap: 16px; flex-wrap: wrap; }
    .row mat-form-field { flex: 1; min-width: 180px; }
    .label { display: flex; align-items: center; gap: 8px; margin: 0 0 8px; font: var(--mat-sys-body-medium); }
    .notes { margin-top: 16px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class SiteDialog {
  private readonly api = inject(CustomersApi);
  private readonly ref = inject(MatDialogRef<SiteDialog, boolean>);
  protected readonly data = inject<SiteDialogData>(MAT_DIALOG_DATA);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly location = signal<LatLng | null>(
    this.data.site?.latitude != null && this.data.site.longitude != null
      ? { lat: this.data.site.latitude, lng: this.data.site.longitude }
      : null,
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.site?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    addressLine1: [this.data.site?.addressLine1 ?? '', [Validators.required, Validators.maxLength(200)]],
    addressLine2: [this.data.site?.addressLine2 ?? ''],
    city: [this.data.site?.city ?? '', [Validators.required, Validators.maxLength(100)]],
    region: [this.data.site?.region ?? ''],
    accessNotes: [this.data.site?.accessNotes ?? ''],
  });

  protected save(): void {
    const v = this.form.getRawValue();
    const pin = this.location();
    const input = {
      ...v,
      addressLine2: v.addressLine2 || null,
      region: v.region || null,
      accessNotes: v.accessNotes || null,
      country: this.data.site?.country ?? null,
      latitude: pin?.lat ?? null,
      longitude: pin?.lng ?? null,
    };
    const { customerId, site } = this.data;
    const request = site ? this.api.updateSite(site.id, input) : this.api.addSite(customerId, input);
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
