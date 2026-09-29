import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { problemMessage } from '../../../core/problem';
import { SignaturePad } from '../../../shared/ui/signature-pad/signature-pad';
import { WorkOrder } from '../../work-orders/work-orders.api';
import { FieldApi } from '../field.api';

/** US-TAPP-08: notes, skipped-task reason when needed, the signer's name and signature. */
@Component({
  selector: 'app-complete-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, SignaturePad],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Complete {{ workOrder.number }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline">
          <mat-label>What was done</mat-label>
          <textarea matInput formControlName="completionNotes" rows="4"></textarea>
        </mat-form-field>
        @if (openTasks > 0) {
          <mat-form-field appearance="outline">
            <mat-label>Why {{ openTasks }} task(s) were skipped</mat-label>
            <textarea matInput formControlName="skippedTasksReason" rows="2"></textarea>
            <mat-hint>Some checklist tasks are not ticked.</mat-hint>
          </mat-form-field>
        }
        <mat-form-field appearance="outline">
          <mat-label>Customer name</mat-label>
          <input matInput formControlName="signedByName" autocomplete="off" />
        </mat-form-field>
        <app-signature-pad #pad />
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || pad.empty() || busy()">Complete job</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; gap: 4px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class CompleteDialog {
  protected readonly workOrder = inject<WorkOrder>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<CompleteDialog, WorkOrder>);
  private readonly api = inject(FieldApi);
  private readonly pad = viewChild.required(SignaturePad);
  protected readonly openTasks = this.workOrder.tasks.filter((t) => !t.isDone).length;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Kept after upload so a retry after a failed completion does not upload the signature twice. */
  private signatureId: string | null = null;

  protected readonly form = inject(NonNullableFormBuilder).group({
    completionNotes: ['', [Validators.required, Validators.maxLength(4000)]],
    skippedTasksReason: ['', this.openTasks > 0 ? [Validators.required, Validators.maxLength(1000)] : []],
    signedByName: ['', [Validators.required, Validators.maxLength(200)]],
  });

  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      if (!this.signatureId) {
        const png = await this.pad().toBlob();
        if (!png) throw new Error('Ask the customer to sign.');
        this.signatureId = (await firstValueFrom(this.api.upload(this.workOrder.id, 'Signature', png, 'signature.png'))).id;
      }
      const v = this.form.getRawValue();
      const done = await firstValueFrom(this.api.complete(this.workOrder.id, {
        completionNotes: v.completionNotes.trim(),
        signedByName: v.signedByName.trim(),
        signatureAttachmentId: this.signatureId,
        skippedTasksReason: this.openTasks > 0 ? v.skippedTasksReason.trim() : null,
      }));
      this.ref.close(done);
    } catch (err: unknown) {
      this.error.set(err instanceof HttpErrorResponse || !(err instanceof Error) ? problemMessage(err) : err.message);
      this.busy.set(false);
    }
  }
}
