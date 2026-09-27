import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../core/problem';
import { Technician, TechniciansApi } from './technicians.api';

export const HEX_COLOR = /^#[0-9A-Fa-f]{6}$/;

/** Turns `08:00` (from a time input) into the `08:00:00` the API expects. */
export function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

@Component({
  selector: 'app-technician-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Edit {{ technician.fullName }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <div class="row">
          <mat-form-field appearance="outline"><mat-label>Employee code</mat-label><input matInput formControlName="employeeCode" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Phone</mat-label><input matInput formControlName="phone" /></mat-form-field>
        </div>
        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Color</mat-label>
            <input matInput formControlName="color" />
            <input matSuffix type="color" class="picker" aria-label="Pick a color" [value]="form.controls.color.value" (input)="pick($any($event.target).value)" />
            <mat-hint>Shown on the dispatch board, e.g. #1E88E5</mat-hint>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Hourly cost (AED)</mat-label><input matInput type="number" min="0" step="0.01" formControlName="hourlyCost" /></mat-form-field>
        </div>
        <div class="row">
          <mat-form-field appearance="outline"><mat-label>Working hours start</mat-label><input matInput type="time" formControlName="workingHoursStart" /></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Working hours end</mat-label><input matInput type="time" formControlName="workingHoursEnd" /></mat-form-field>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Skills</mat-label>
          <mat-select formControlName="skillIds" multiple>
            @for (skill of skills.value() ?? []; track skill.id) {
              <mat-option [value]="skill.id">{{ skill.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (hoursInvalid()) {
          <p class="error" role="alert">Working hours must end after they start.</p>
        }
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || hoursInvalid() || busy()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(520px, 80vw); }
    .row { display: flex; gap: 16px; flex-wrap: wrap; }
    .row mat-form-field { flex: 1; min-width: 180px; }
    .picker { width: 32px; height: 28px; border: none; background: none; padding: 0; margin-right: 8px; cursor: pointer; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class TechnicianDialog {
  private readonly api = inject(TechniciansApi);
  private readonly ref = inject(MatDialogRef<TechnicianDialog, boolean>);
  protected readonly technician = inject<Technician>(MAT_DIALOG_DATA);
  protected readonly skills = rxResource({ stream: () => this.api.skills() });
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    employeeCode: [this.technician.employeeCode, [Validators.required, Validators.maxLength(20)]],
    phone: [this.technician.phone ?? '', Validators.maxLength(30)],
    color: [this.technician.color, [Validators.required, Validators.pattern(HEX_COLOR)]],
    hourlyCost: [this.technician.hourlyCost, [Validators.required, Validators.min(0)]],
    workingHoursStart: [this.technician.workingHoursStart.slice(0, 5), Validators.required],
    workingHoursEnd: [this.technician.workingHoursEnd.slice(0, 5), Validators.required],
    skillIds: [this.technician.skills.map((s) => s.id)],
  });

  protected hoursInvalid(): boolean {
    const { workingHoursStart, workingHoursEnd } = this.form.getRawValue();
    return !!workingHoursStart && !!workingHoursEnd && workingHoursEnd <= workingHoursStart;
  }

  protected pick(color: string): void {
    this.form.controls.color.setValue(color.toUpperCase());
  }

  protected save(): void {
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.api
      .update(this.technician.id, {
        ...v,
        phone: v.phone || null,
        workingHoursStart: toApiTime(v.workingHoursStart),
        workingHoursEnd: toApiTime(v.workingHoursEnd),
      })
      .subscribe({
        next: () => this.ref.close(true),
        error: (err: unknown) => {
          this.error.set(problemMessage(err));
          this.busy.set(false);
        },
      });
  }
}
