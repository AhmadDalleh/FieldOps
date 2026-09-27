import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../core/problem';
import { ASSET_TYPES, Asset, AssetStatus, AssetType, AssetsApi } from './assets.api';

export interface AssetDialogData {
  /** Active sites the asset can be registered at (ignored when editing). */
  sites: { id: string; name: string }[];
  asset: Asset | null;
}

@Component({
  selector: 'app-asset-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.asset ? 'Edit asset' : 'New asset' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        @if (!data.asset) {
          <mat-form-field appearance="outline">
            <mat-label>Site</mat-label>
            <mat-select formControlName="siteId">
              @for (site of data.sites; track site.id) {
                <mat-option [value]="site.id">{{ site.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Type</mat-label>
            <mat-select formControlName="assetType">
              @for (type of types; track type) {
                <mat-option [value]="type">{{ type }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="name" /></mat-form-field>
        </div>
        <div class="row">
          <mat-form-field appearance="outline"><mat-label>Manufacturer</mat-label><input matInput formControlName="manufacturer" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Model</mat-label><input matInput formControlName="model" /></mat-form-field>
        </div>
        <mat-form-field appearance="outline"><mat-label>Serial number</mat-label><input matInput formControlName="serialNumber" /></mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline"><mat-label>Install date</mat-label><input matInput type="date" formControlName="installDate" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Warranty until</mat-label><input matInput type="date" formControlName="warrantyExpiresOn" /></mat-form-field>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <mat-select formControlName="status">
            <mat-option value="Active">Active</mat-option>
            <mat-option value="OutOfService">Out of service</mat-option>
            <mat-option value="Retired">Retired</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Notes</mat-label><textarea matInput formControlName="notes"></textarea></mat-form-field>
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
    .fields { display: flex; flex-direction: column; min-width: min(520px, 80vw); }
    .row { display: flex; gap: 16px; flex-wrap: wrap; }
    .row mat-form-field { flex: 1; min-width: 180px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class AssetDialog {
  private readonly api = inject(AssetsApi);
  private readonly ref = inject(MatDialogRef<AssetDialog, boolean>);
  protected readonly data = inject<AssetDialogData>(MAT_DIALOG_DATA);
  protected readonly types = ASSET_TYPES;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly asset = this.data.asset;
  protected readonly form = inject(NonNullableFormBuilder).group({
    siteId: [this.asset?.siteId ?? (this.data.sites.length === 1 ? this.data.sites[0].id : ''), Validators.required],
    assetType: [this.asset?.assetType ?? ('AC' as AssetType), Validators.required],
    name: [this.asset?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    manufacturer: [this.asset?.manufacturer ?? ''],
    model: [this.asset?.model ?? ''],
    serialNumber: [this.asset?.serialNumber ?? ''],
    installDate: [this.asset?.installDate ?? ''],
    warrantyExpiresOn: [this.asset?.warrantyExpiresOn ?? ''],
    status: [this.asset?.status ?? ('Active' as AssetStatus)],
    notes: [this.asset?.notes ?? ''],
  });

  protected save(): void {
    const { siteId, ...v } = this.form.getRawValue();
    const input = {
      ...v,
      manufacturer: v.manufacturer || null,
      model: v.model || null,
      serialNumber: v.serialNumber || null,
      installDate: v.installDate || null,
      warrantyExpiresOn: v.warrantyExpiresOn || null,
      notes: v.notes || null,
    };
    const request = this.asset ? this.api.update(this.asset.id, input) : this.api.register(siteId, input);
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
