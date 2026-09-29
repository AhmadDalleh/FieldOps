import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { Observable, filter } from 'rxjs';
import { problemMessage } from '../../core/problem';
import { DubaiTimePipe } from '../../shared/time/dubai-time.pipe';
import { MapPicker } from '../../shared/ui/map-picker/map-picker';
import { WarrantyBadge } from '../../shared/ui/warranty-badge/warranty-badge';
import { AssetHistoryDialog } from '../assets/asset-history-dialog';
import { ReasonDialog, ReasonDialogData } from './reason-dialog';
import { PriorityChip, StatusChip, move, statusLabel } from './work-order-labels';
import { WorkOrderDialog, WorkOrderDialogData } from './work-order-dialog';
import { Note, WorkOrder, WorkOrderTask, WorkOrdersApi } from './work-orders.api';

@Component({
  selector: 'app-work-order-detail',
  imports: [
    FormsModule, RouterLink, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatSlideToggleModule, DubaiTimePipe, MapPicker, WarrantyBadge, StatusChip, PriorityChip,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/office/work-orders" class="back">← Work orders</a>
    @if (workOrder(); as w) {
      <header class="header">
        <div>
          <h1>{{ w.number }} · {{ w.title }}</h1>
          <div class="meta">
            <app-status-chip [status]="w.status" />
            <app-priority-chip [priority]="w.priority" />
            <span>{{ w.type }}</span>
            @if (w.isOverdue) {
              <span class="overdue">Overdue</span>
            }
          </div>
        </div>
        <div class="actions">
          @if (w.isEditable) {
            <button mat-stroked-button (click)="edit(w)">Edit</button>
          }
          @if (can('Hold')) {
            <button mat-stroked-button (click)="hold(w)">Put on hold</button>
          }
          @if (can('Resume')) {
            <button mat-flat-button (click)="run(api.resume(w.id), 'Resumed.')">Resume</button>
          }
          @if (can('Cancel')) {
            <button mat-button class="danger" (click)="cancel(w)">Cancel job</button>
          }
        </div>
      </header>

      <div class="grid">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Details</mat-card-title></mat-card-header>
          <mat-card-content>
            <dl class="info">
              <dt>Customer</dt>
              <dd><a [routerLink]="['/office/customers', w.customer.id]">{{ w.customer.name }}</a> · {{ w.customer.phone }}</dd>
              <dt>Site</dt>
              <dd>
                {{ w.site.name }}<br />{{ w.site.addressLine1 }}{{ w.site.addressLine2 ? ', ' + w.site.addressLine2 : '' }}, {{ w.site.city }}
                @if (w.site.accessNotes) {
                  <div class="muted">{{ w.site.accessNotes }}</div>
                }
              </dd>
              <dt>Asset</dt>
              <dd>
                @if (w.asset; as a) {
                  {{ a.name }}
                  <span class="muted">{{ assetLine(a) }}</span>
                  <div><app-warranty-badge [expiresOn]="a.warrantyExpiresOn" [underWarranty]="a.underWarranty" /></div>
                  <button mat-button (click)="assetHistory(w)">Service history</button>
                } @else {
                  —
                }
              </dd>
              <dt>Technician</dt>
              <dd>
                @if (w.technician; as t) {
                  <span class="swatch" [style.background]="t.color"></span>{{ t.name }}
                } @else {
                  Unassigned
                }
              </dd>
              <dt>Scheduled</dt>
              <dd>{{ w.scheduledStart ? (w.scheduledStart | dubaiTime) + ' to ' + (w.scheduledEnd | dubaiTime) : '—' }}</dd>
              <dt>Due by</dt>
              <dd [class.overdue]="w.isOverdue">{{ w.dueBy ? (w.dueBy | dubaiTime) : '—' }}</dd>
              <dt>Created</dt>
              <dd>{{ w.createdAt | dubaiTime }}</dd>
              @if (w.description) {
                <dt>Description</dt>
                <dd class="pre">{{ w.description }}</dd>
              }
              @if (w.cancelReason) {
                <dt>Cancel reason</dt>
                <dd>{{ w.cancelReason }}</dd>
              }
              @if (w.completionNotes) {
                <dt>Completion notes</dt>
                <dd class="pre">{{ w.completionNotes }}</dd>
                <dt>Signed by</dt>
                <dd>{{ w.signedByName }}</dd>
              }
            </dl>
            @if (pin(); as p) {
              <app-map-picker [value]="p" [readonly]="true" />
            }
          </mat-card-content>
        </mat-card>

        <div class="column">
          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>Tasks {{ doneCount() }}/{{ w.tasks.length }}</mat-card-title></mat-card-header>
            <mat-card-content>
              <ul class="tasks">
                @for (t of w.tasks; track t.id; let i = $index; let last = $last) {
                  <li>
                    @if (editingTask() === t.id) {
                      <input class="task-edit" aria-label="Task description" [(ngModel)]="taskText" (keydown.enter)="saveTask(w, t)" (keydown.escape)="editingTask.set(null)" />
                      <button mat-button (click)="saveTask(w, t)">Save</button>
                    } @else {
                      <mat-checkbox [checked]="t.isDone" [disabled]="locked()" (change)="run(api.toggleTask(w.id, t.id))">
                        <span [class.done]="t.isDone">{{ t.description }}</span>
                      </mat-checkbox>
                      @if (t.isDone && t.doneByName) {
                        <span class="muted small">{{ t.doneByName }}, {{ t.doneAt | dubaiTime }}</span>
                      }
                      @if (!locked()) {
                        <span class="task-actions">
                          <button mat-icon-button aria-label="Move up" [disabled]="i === 0" (click)="reorder(w, i, -1)"><mat-icon>arrow_upward</mat-icon></button>
                          <button mat-icon-button aria-label="Move down" [disabled]="last" (click)="reorder(w, i, 1)"><mat-icon>arrow_downward</mat-icon></button>
                          <button mat-icon-button aria-label="Edit task" (click)="startTaskEdit(t)"><mat-icon>edit</mat-icon></button>
                          <button mat-icon-button aria-label="Remove task" (click)="run(api.removeTask(w.id, t.id))"><mat-icon>delete</mat-icon></button>
                        </span>
                      }
                    }
                  </li>
                } @empty {
                  <li class="muted">No tasks.</li>
                }
              </ul>
              @if (!locked()) {
                <form class="add" (ngSubmit)="addTask(w)">
                  <mat-form-field appearance="outline" subscriptSizing="dynamic">
                    <mat-label>New task</mat-label>
                    <input matInput name="task" [(ngModel)]="newTask" />
                  </mat-form-field>
                  <button mat-stroked-button type="submit" [disabled]="!newTask().trim()">Add</button>
                </form>
              }
            </mat-card-content>
          </mat-card>

          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>Notes</mat-card-title></mat-card-header>
            <mat-card-content>
              @for (n of notes.value() ?? []; track n.id) {
                <div class="note">
                  <div class="note-head">
                    <strong>{{ n.authorName }}</strong>
                    <span class="muted small">{{ n.createdAt | dubaiTime }}{{ n.updatedAt ? ' · edited' : '' }}</span>
                    @if (n.isInternal) {
                      <span class="internal">Internal</span>
                    }
                    @if (n.canEdit && editingNote() !== n.id) {
                      <button mat-button (click)="startNoteEdit(n)">Edit</button>
                    }
                  </div>
                  @if (editingNote() === n.id) {
                    <textarea class="note-edit" aria-label="Note" [(ngModel)]="noteEditText" rows="3"></textarea>
                    <button mat-button (click)="saveNote(w, n)" [disabled]="!noteEditText().trim()">Save</button>
                    <button mat-button (click)="editingNote.set(null)">Cancel</button>
                  } @else {
                    <p class="pre">{{ n.body }}</p>
                  }
                </div>
              } @empty {
                <p class="muted">No notes yet.</p>
              }
              <form class="note-form" (ngSubmit)="addNote(w)">
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>Add a note</mat-label>
                  <textarea matInput name="note" [(ngModel)]="newNote" rows="2"></textarea>
                </mat-form-field>
                <div class="note-actions">
                  <mat-slide-toggle name="internal" [(ngModel)]="newNoteInternal">Internal (hidden from the customer)</mat-slide-toggle>
                  <button mat-stroked-button type="submit" [disabled]="!newNote().trim()">Add note</button>
                </div>
              </form>
            </mat-card-content>
          </mat-card>

          <mat-card appearance="outlined">
            <mat-card-header><mat-card-title>Timeline</mat-card-title></mat-card-header>
            <mat-card-content>
              <ol class="timeline">
                @for (h of history.value() ?? []; track $index) {
                  <li>
                    <strong>{{ h.fromStatus ? label(h.fromStatus) + ' → ' : '' }}{{ label(h.toStatus) }}</strong>
                    <span class="muted small">{{ h.changedByName }} · {{ h.changedAt | dubaiTime }}</span>
                    @if (h.note) {
                      <div class="pre">{{ h.note }}</div>
                    }
                  </li>
                }
              </ol>
            </mat-card-content>
          </mat-card>
        </div>
      </div>
    } @else if (loaded.error()) {
      <p class="error">{{ errorMessage() }}</p>
    }
  `,
  styles: `
    .back { display: inline-block; margin-bottom: 8px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; flex-wrap: wrap; }
    .header h1 { margin: 0 0 8px; }
    .meta { display: flex; gap: 12px; align-items: center; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .danger { color: var(--mat-sys-error); }
    .grid { display: grid; grid-template-columns: minmax(320px, 1fr) minmax(360px, 1.2fr); gap: 16px; margin-top: 16px; align-items: start; }
    @media (max-width: 1000px) { .grid { grid-template-columns: 1fr; } }
    .column { display: flex; flex-direction: column; gap: 16px; }
    .info { display: grid; grid-template-columns: 120px 1fr; gap: 8px 12px; margin: 0 0 16px; }
    .info dt { color: var(--mat-sys-on-surface-variant); }
    .info dd { margin: 0; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .small { font: var(--mat-sys-body-small); margin-left: 8px; }
    .pre { white-space: pre-wrap; margin: 4px 0 0; }
    .overdue { color: var(--mat-sys-error); font-weight: 600; }
    .swatch { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 6px; }
    .tasks { list-style: none; padding: 0; margin: 0 0 12px; }
    .tasks li { display: flex; align-items: center; flex-wrap: wrap; border-bottom: 1px solid var(--mat-sys-outline-variant); min-height: 48px; }
    .task-actions { margin-left: auto; }
    .task-edit { flex: 1; font: inherit; padding: 6px; }
    .done { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .add { display: flex; gap: 8px; align-items: center; }
    .add mat-form-field { flex: 1; }
    .note { border-bottom: 1px solid var(--mat-sys-outline-variant); padding: 8px 0; }
    .note-head { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; }
    .note-edit { width: 100%; font: inherit; }
    .internal { font: var(--mat-sys-label-small); padding: 1px 8px; border-radius: 10px; background: var(--mat-sys-tertiary-container); margin-left: 8px; }
    .note-form { margin-top: 12px; }
    .note-form mat-form-field { width: 100%; }
    .note-actions { display: flex; justify-content: space-between; align-items: center; margin-top: 8px; gap: 8px; flex-wrap: wrap; }
    .timeline { padding-left: 20px; margin: 0; }
    .timeline li { margin-bottom: 8px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class WorkOrderDetail {
  protected readonly api = inject(WorkOrdersApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  readonly id = input.required<string>();
  protected readonly label = statusLabel;

  protected readonly loaded = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });
  /** The latest copy, from loading or from an action's response. */
  protected readonly workOrder = linkedSignal<WorkOrder | null>(() => this.loaded.value() ?? null);
  protected readonly notes = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.notes(params) });
  protected readonly history = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.history(params) });

  protected readonly locked = computed(() => {
    const status = this.workOrder()?.status;
    return status === 'Completed' || status === 'Invoiced' || status === 'Cancelled';
  });
  protected readonly doneCount = computed(() => this.workOrder()?.tasks.filter((t) => t.isDone).length ?? 0);
  protected readonly pin = computed(() => {
    const site = this.workOrder()?.site;
    return site?.latitude != null && site.longitude != null ? { lat: site.latitude, lng: site.longitude } : null;
  });
  protected readonly errorMessage = computed(() => problemMessage(this.loaded.error()));

  protected readonly newTask = signal('');
  protected readonly editingTask = signal<string | null>(null);
  protected readonly taskText = signal('');
  protected readonly newNote = signal('');
  protected readonly newNoteInternal = signal(false);
  protected readonly editingNote = signal<string | null>(null);
  protected readonly noteEditText = signal('');

  protected assetLine(a: NonNullable<WorkOrder['asset']>): string {
    return [a.manufacturer, a.model, a.serialNumber].filter((x) => !!x).join(' · ');
  }

  protected can(action: string): boolean {
    return this.workOrder()?.allowedActions.includes(action as never) ?? false;
  }

  /** Runs a call that returns the updated work order, then refreshes the page state. */
  protected run(request: Observable<WorkOrder>, done?: string): void {
    request.subscribe({
      next: (wo) => {
        this.workOrder.set(wo);
        this.history.reload();
        if (done) this.snackBar.open(done, 'Close', { duration: 3000 });
      },
      error: (err: unknown) => {
        this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 });
        this.refresh();
      },
    });
  }

  protected edit(w: WorkOrder): void {
    this.dialog
      .open<WorkOrderDialog, WorkOrderDialogData, WorkOrder>(WorkOrderDialog, { data: { workOrder: w } })
      .afterClosed()
      .subscribe((saved) => (saved ? this.workOrder.set(saved) : this.refresh()));
  }

  protected hold(w: WorkOrder): void {
    this.ask({ title: `Put ${w.number} on hold`, label: 'Why is the job on hold?', confirm: 'Put on hold' }).subscribe((note) =>
      this.run(this.api.hold(w.id, note), 'Job is on hold.'),
    );
  }

  protected cancel(w: WorkOrder): void {
    this.ask({ title: `Cancel ${w.number}`, label: 'Reason for cancelling', confirm: 'Cancel job', danger: true }).subscribe((reason) =>
      this.run(this.api.cancel(w.id, reason), 'Job cancelled.'),
    );
  }

  protected assetHistory(w: WorkOrder): void {
    this.dialog.open(AssetHistoryDialog, { data: w.asset });
  }

  protected addTask(w: WorkOrder): void {
    this.run(this.api.addTask(w.id, this.newTask().trim()));
    this.newTask.set('');
  }

  protected startTaskEdit(t: WorkOrderTask): void {
    this.taskText.set(t.description);
    this.editingTask.set(t.id);
  }

  protected saveTask(w: WorkOrder, t: WorkOrderTask): void {
    const text = this.taskText().trim();
    this.editingTask.set(null);
    if (text && text !== t.description) this.run(this.api.updateTask(w.id, t.id, text));
  }

  protected reorder(w: WorkOrder, index: number, delta: -1 | 1): void {
    this.run(this.api.reorderTasks(w.id, move(w.tasks, index, delta).map((t) => t.id)));
  }

  protected addNote(w: WorkOrder): void {
    this.api.addNote(w.id, this.newNote().trim(), this.newNoteInternal()).subscribe({
      next: () => {
        this.newNote.set('');
        this.newNoteInternal.set(false);
        this.notes.reload();
      },
      error: (err: unknown) => this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 }),
    });
  }

  protected startNoteEdit(n: Note): void {
    this.noteEditText.set(n.body);
    this.editingNote.set(n.id);
  }

  protected saveNote(w: WorkOrder, n: Note): void {
    this.api.editNote(w.id, n.id, this.noteEditText().trim(), n.isInternal).subscribe({
      next: () => {
        this.editingNote.set(null);
        this.notes.reload();
      },
      error: (err: unknown) => {
        this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 });
        this.editingNote.set(null);
        this.notes.reload();
      },
    });
  }

  private refresh(): void {
    this.workOrder.set(null);
    this.loaded.reload();
    this.history.reload();
  }

  private ask(data: ReasonDialogData): Observable<string> {
    return this.dialog
      .open<ReasonDialog, ReasonDialogData, string>(ReasonDialog, { data })
      .afterClosed()
      .pipe(filter((text): text is string => !!text));
  }
}
