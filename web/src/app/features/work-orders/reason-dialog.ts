import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface ReasonDialogData {
  title: string;
  label: string;
  confirm: string;
  danger?: boolean;
}

/** Asks for a required piece of text, such as a hold note or a cancel reason. Closes with the trimmed text. */
@Component({
  selector: 'app-reason-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <mat-form-field appearance="outline" class="field">
          <mat-label>{{ data.label }}</mat-label>
          <textarea matInput [formControl]="text" maxlength="500" cdkFocusInitial></textarea>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Back</button>
        <button mat-flat-button type="submit" [class.danger]="data.danger" [disabled]="text.invalid">{{ data.confirm }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .field { width: min(420px, 80vw); }
    .danger { background: var(--mat-sys-error); color: var(--mat-sys-on-error); }
  `,
})
export class ReasonDialog {
  private readonly ref = inject(MatDialogRef<ReasonDialog, string>);
  protected readonly data = inject<ReasonDialogData>(MAT_DIALOG_DATA);
  protected readonly text = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/\S/)] });
  protected readonly form = new FormGroup({ text: this.text });

  protected submit(): void {
    if (this.text.valid) this.ref.close(this.text.value.trim());
  }
}
