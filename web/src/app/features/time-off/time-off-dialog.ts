import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../core/problem';
import { dubaiLocalToUtc } from '../../shared/time/dubai-time';
import { TimeOffApi } from './time-off.api';

export interface TimeOffDialogData {
  /** Technicians to choose from when office staff book time off; omitted for technicians. */
  technicians?: { id: string; name: string }[];
}

function endsAfterStart(group: AbstractControl): ValidationErrors | null {
  const { startsAt, endsAt } = group.value as { startsAt: string; endsAt: string };
  return startsAt && endsAt && endsAt <= startsAt ? { endsBeforeStart: true } : null;
}

@Component({
  selector: 'app-time-off-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Request time off</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        @if (data.technicians) {
          <mat-form-field appearance="outline">
            <mat-label>Technician</mat-label>
            <mat-select formControlName="technicianId">
              @for (t of data.technicians; track t.id) {
                <mat-option [value]="t.id">{{ t.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <mat-form-field appearance="outline">
          <mat-label>From (Dubai time)</mat-label>
          <input matInput type="datetime-local" formControlName="startsAt" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>To (Dubai time)</mat-label>
          <input matInput type="datetime-local" formControlName="endsAt" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Reason</mat-label>
          <textarea matInput formControlName="reason" maxlength="500"></textarea>
        </mat-form-field>
        @if (form.hasError('endsBeforeStart')) {
          <p class="error" role="alert">The end must be after the start.</p>
        }
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">Submit</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(420px, 80vw); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class TimeOffDialog {
  private readonly api = inject(TimeOffApi);
  private readonly ref = inject(MatDialogRef<TimeOffDialog, boolean>);
  protected readonly data = inject<TimeOffDialogData>(MAT_DIALOG_DATA, { optional: true }) ?? {};
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      technicianId: ['', this.data.technicians ? Validators.required : []],
      startsAt: ['', Validators.required],
      endsAt: ['', Validators.required],
      reason: [''],
    },
    { validators: endsAfterStart },
  );

  protected save(): void {
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.api
      .request({
        startsAt: dubaiLocalToUtc(v.startsAt),
        endsAt: dubaiLocalToUtc(v.endsAt),
        reason: v.reason.trim() || null,
        technicianId: v.technicianId || null,
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
