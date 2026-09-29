import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../core/problem';
import { dubaiLocalToUtc, dubaiToday, utcToDubaiLocal } from '../../shared/time/dubai-time';
import { TechniciansApi } from '../technicians/technicians.api';
import { ScheduleFlow } from './schedule-flow';
import { WorkOrder } from './work-orders.api';

const HOUR_MS = 60 * 60 * 1000;

/** The next full hour as a Dubai `datetime-local` value. */
export function nextFullHour(now = new Date()): string {
  return utcToDubaiLocal(new Date(Math.ceil(now.getTime() / HOUR_MS) * HOUR_MS).toISOString());
}

/** Adds hours to a Dubai `datetime-local` value. */
export function addHoursLocal(local: string, hours: number): string {
  return utcToDubaiLocal(new Date(new Date(dubaiLocalToUtc(local)).getTime() + hours * HOUR_MS).toISOString());
}

@Component({
  selector: 'app-schedule-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ workOrder.technician ? 'Reschedule' : 'Schedule' }} {{ workOrder.number }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline">
          <mat-label>Technician</mat-label>
          <mat-select formControlName="technicianId">
            @for (t of technicians(); track t.id) {
              <mat-option [value]="t.id">{{ t.fullName }}{{ t.skills.length ? ' · ' + skillNames(t.skills) : '' }}</mat-option>
            }
          </mat-select>
          @if (missingSkill()) {
            <mat-hint class="warn">This technician does not have {{ workOrder.requiredSkill?.name }}.</mat-hint>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Start (Dubai time)</mat-label>
          <input matInput type="datetime-local" formControlName="start" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>End (Dubai time)</mat-label>
          <input matInput type="datetime-local" formControlName="end" />
        </mat-form-field>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">Schedule</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(420px, 80vw); }
    .warn { color: #b45309; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class ScheduleDialog {
  protected readonly workOrder = inject<WorkOrder>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ScheduleDialog, WorkOrder>);
  private readonly flow = inject(ScheduleFlow);
  private readonly techniciansApi = inject(TechniciansApi);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly start = this.workOrder.scheduledStart ? utcToDubaiLocal(this.workOrder.scheduledStart) : nextFullHour();
  protected readonly form = inject(NonNullableFormBuilder).group({
    technicianId: [this.workOrder.technician?.id ?? '', Validators.required],
    start: [this.start, Validators.required],
    end: [this.workOrder.scheduledEnd ? utcToDubaiLocal(this.workOrder.scheduledEnd) : addHoursLocal(this.start, 2), Validators.required],
  });

  private readonly availability = rxResource({ stream: () => this.techniciansApi.list(dubaiToday()) });
  protected readonly technicians = computed(() => (this.availability.value() ?? []).map((a) => a.technician));
  private readonly technicianId = toSignal(this.form.controls.technicianId.valueChanges, {
    initialValue: this.form.controls.technicianId.value,
  });
  protected readonly missingSkill = computed(() => {
    const skill = this.workOrder.requiredSkill;
    const tech = this.technicians().find((t) => t.id === this.technicianId());
    return !!skill && !!tech && !tech.skills.some((s) => s.id === skill.id);
  });

  constructor() {
    // Keep the slot two hours long when the start moves past the end.
    this.form.controls.start.valueChanges.subscribe((start) => {
      if (start && start >= this.form.controls.end.value) this.form.controls.end.setValue(addHoursLocal(start, 2));
    });
  }

  protected skillNames(skills: { name: string }[]): string {
    return skills.map((s) => s.name).join(', ');
  }

  protected save(): void {
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.flow
      .schedule(this.workOrder.id, { technicianId: v.technicianId, start: dubaiLocalToUtc(v.start), end: dubaiLocalToUtc(v.end) })
      .subscribe({
        next: (result) => this.ref.close(result.workOrder),
        error: (err: unknown) => {
          this.error.set(problemMessage(err));
          this.busy.set(false);
        },
        complete: () => this.busy.set(false),
      });
  }
}
