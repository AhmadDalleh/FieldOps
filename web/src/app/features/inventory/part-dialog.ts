import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { problemMessage } from '../../core/problem';
import { InventoryApi, Part, PartUnit, UNITS } from './inventory.api';

/** US-INV-01: create or edit a catalog part (Admin). */
@Component({
  selector: 'app-part-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ part ? 'Edit ' + part.sku : 'New part' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <div class="row">
          <mat-form-field appearance="outline"><mat-label>SKU</mat-label><input matInput formControlName="sku" /></mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Unit</mat-label>
            <mat-select formControlName="unit">
              @for (u of units; track u.value) {
                <mat-option [value]="u.value">{{ u.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput formControlName="name" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Description</mat-label><textarea matInput formControlName="description" rows="2"></textarea></mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline"><mat-label>Unit cost (AED)</mat-label><input matInput type="number" min="0" step="0.01" formControlName="unitCost" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Unit price (AED)</mat-label><input matInput type="number" min="0" step="0.01" formControlName="unitPrice" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Reorder level</mat-label><input matInput type="number" min="0" step="0.01" formControlName="reorderLevel" /></mat-form-field>
        </div>
        @if (part) {
          <mat-slide-toggle formControlName="isActive">Active</mat-slide-toggle>
        }
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">{{ part ? 'Save' : 'Create' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(560px, 80vw); }
    .row { display: flex; gap: 12px; flex-wrap: wrap; }
    .row mat-form-field { flex: 1; min-width: 140px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class PartDialog {
  protected readonly part = inject<Part | null>(MAT_DIALOG_DATA);
  private readonly api = inject(InventoryApi);
  private readonly ref = inject(MatDialogRef<PartDialog, Part>);
  protected readonly units = UNITS;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly money = [Validators.required, Validators.min(0), Validators.max(999_999_999)];
  protected readonly form = inject(NonNullableFormBuilder).group({
    sku: [this.part?.sku ?? '', [Validators.required, Validators.maxLength(50)]],
    name: [this.part?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.part?.description ?? '', Validators.maxLength(2000)],
    unit: [this.part?.unit ?? ('Pcs' as PartUnit), Validators.required],
    unitCost: [this.part?.unitCost ?? 0, this.money],
    unitPrice: [this.part?.unitPrice ?? 0, this.money],
    reorderLevel: [this.part?.reorderLevel ?? 0, this.money],
    isActive: [this.part?.isActive ?? true],
  });

  protected save(): void {
    const v = this.form.getRawValue();
    const input = { ...v, sku: v.sku.trim(), name: v.name.trim(), description: v.description.trim() || null };
    this.busy.set(true);
    (this.part ? this.api.updatePart(this.part.id, input) : this.api.createPart(input)).subscribe({
      next: (part) => this.ref.close(part),
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
