import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EMPTY, Observable, catchError, switchMap, tap, throwError } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../../shared/ui/confirm-dialog/confirm-dialog';
import { ScheduleInput, ScheduleResult, WorkOrdersApi } from './work-orders.api';

/**
 * Schedules a work order the way US-DSP-01 describes: an overlap with another job asks the dispatcher
 * and retries with `allowOverlap`, and warnings (overlap, missing skill) show in a snack bar.
 * Completes without a value when the dispatcher declines the overlap; other errors pass through.
 */
@Injectable({ providedIn: 'root' })
export class ScheduleFlow {
  private readonly api = inject(WorkOrdersApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  schedule(id: string, input: ScheduleInput): Observable<ScheduleResult> {
    return this.api.schedule(id, input).pipe(
      catchError((err: unknown) => {
        if (!(err instanceof HttpErrorResponse) || err.error?.code !== 'Schedule.Overlap') return throwError(() => err);
        return this.dialog
          .open<ConfirmDialog, ConfirmData, boolean>(ConfirmDialog, {
            data: { title: 'Overlapping jobs', message: err.error.title, confirmLabel: 'Schedule anyway' },
          })
          .afterClosed()
          .pipe(switchMap((yes) => (yes ? this.api.schedule(id, { ...input, allowOverlap: true }) : EMPTY)));
      }),
      tap((result) => {
        if (result.warnings.length) this.snackBar.open(result.warnings.join(' · '), 'Close', { duration: 6000 });
      }),
    );
  }
}
