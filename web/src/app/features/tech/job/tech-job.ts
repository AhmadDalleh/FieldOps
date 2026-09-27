import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { Observable, filter, finalize } from 'rxjs';
import { problemMessage } from '../../../core/problem';
import { currentLocation } from '../../../shared/files/geolocation';
import { MAX_UPLOAD_BYTES, PHOTO_TYPES, compressPhoto, jpegName } from '../../../shared/files/image-compress';
import { SecureImage } from '../../../shared/files/secure-image';
import { dubaiLocalToUtc, utcToDubaiLocal } from '../../../shared/time/dubai-time';
import { DubaiTimePipe } from '../../../shared/time/dubai-time.pipe';
import { WarrantyBadge } from '../../../shared/ui/warranty-badge/warranty-badge';
import { AssetHistoryDialog } from '../../assets/asset-history-dialog';
import { WorkOrderPartsPanel } from '../../inventory/work-order-parts';
import { ReasonDialog, ReasonDialogData } from '../../work-orders/reason-dialog';
import { PriorityChip, StatusChip } from '../../work-orders/work-order-labels';
import { WorkOrder, WorkOrderStatus, WorkOrdersApi } from '../../work-orders/work-orders.api';
import { FieldApi, Location, TimeEntry } from '../field.api';
import { clockTime } from '../my-jobs/my-jobs';
import { CompleteDialog } from './complete-dialog';
import { onWorkOrderChange } from '../../../core/realtime';

const PHOTO_STATUSES: WorkOrderStatus[] = ['EnRoute', 'InProgress', 'OnHold'];

/** Opens turn-by-turn directions: to the pin when the site has one, otherwise to the address. */
export function directionsUrl(site: WorkOrder['site']): string {
  const destination =
    site.latitude != null && site.longitude != null
      ? `${site.latitude},${site.longitude}`
      : [site.addressLine1, site.addressLine2, site.city].filter((x) => !!x).join(', ');
  return `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(destination)}`;
}

/** Minutes as `1 h 05 min`. */
export function formatMinutes(minutes: number | null): string {
  if (minutes == null) return 'running';
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h ? `${h} h ${m.toString().padStart(2, '0')} min` : `${m} min`;
}

/** US-TAPP-02 to 09: everything the technician needs on site, sized for a phone. */
@Component({
  selector: 'app-tech-job',
  imports: [
    WorkOrderPartsPanel,
    FormsModule, RouterLink, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, DubaiTimePipe,
    StatusChip, PriorityChip, WarrantyBadge, SecureImage,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/tech/my-jobs" class="back">← My jobs</a>
    @if (workOrder(); as w) {
      <header>
        <div class="top">
          <span class="muted">{{ w.number }} · {{ w.type }}</span>
          <app-priority-chip [priority]="w.priority" />
        </div>
        <h1>{{ w.title }}</h1>
        <div class="top">
          <app-status-chip [status]="w.status" />
          @if (w.scheduledStart) {
            <span>{{ time(w.scheduledStart) }}–{{ time(w.scheduledEnd) }}</span>
          }
        </div>
      </header>

      <section class="actions" aria-label="Job actions">
        @if (w.status === 'Dispatched') {
          <button mat-flat-button (click)="enRoute(w)" [disabled]="busy()">
            <mat-icon>directions_car</mat-icon>{{ locating() ? 'Getting your location…' : 'On my way' }}
          </button>
        }
        @if (w.status === 'Dispatched') {
          <button mat-stroked-button (click)="start(w)" [disabled]="busy()"><mat-icon>play_arrow</mat-icon>Start job</button>
        }
        @if (w.status === 'EnRoute') {
          <button mat-flat-button (click)="start(w)" [disabled]="busy()"><mat-icon>play_arrow</mat-icon>I've arrived, start job</button>
        }
        @if (w.status === 'InProgress') {
          <button mat-flat-button (click)="complete(w)" [disabled]="busy()"><mat-icon>task_alt</mat-icon>Complete</button>
          <button mat-stroked-button (click)="hold(w)" [disabled]="busy()"><mat-icon>pause</mat-icon>Put on hold</button>
        }
        @if (w.status === 'OnHold') {
          <button mat-flat-button (click)="run(workOrders.resume(w.id), 'Resumed.')" [disabled]="busy()"><mat-icon>play_arrow</mat-icon>Resume</button>
        }
        @if (w.status === 'Scheduled') {
          <p class="muted">The office has not dispatched this job yet.</p>
        }
      </section>

      <section class="block">
        <h2>Customer</h2>
        <div>{{ w.customer.name }}</div>
        <a mat-stroked-button [href]="'tel:' + w.customer.phone"><mat-icon>call</mat-icon>{{ w.customer.phone }}</a>
      </section>

      <section class="block">
        <h2>Site</h2>
        <div>{{ w.site.name }}</div>
        <div>{{ w.site.addressLine1 }}{{ w.site.addressLine2 ? ', ' + w.site.addressLine2 : '' }}, {{ w.site.city }}</div>
        @if (w.site.accessNotes) {
          <div class="note"><mat-icon>key</mat-icon>{{ w.site.accessNotes }}</div>
        }
        <a mat-stroked-button [href]="directions(w)" target="_blank" rel="noopener"><mat-icon>navigation</mat-icon>Navigate</a>
      </section>

      @if (w.description) {
        <section class="block">
          <h2>Job description</h2>
          <p class="pre">{{ w.description }}</p>
        </section>
      }

      @if (w.asset; as a) {
        <section class="block">
          <h2>Equipment</h2>
          <div>{{ a.name }} · {{ a.assetType }}</div>
          <div class="muted">{{ assetLine(a) }}</div>
          <app-warranty-badge [expiresOn]="a.warrantyExpiresOn" [underWarranty]="a.underWarranty" />
          <button mat-button (click)="assetHistory(w)">Service history</button>
        </section>
      }

      <section class="block">
        <h2>Checklist {{ doneCount() }}/{{ w.tasks.length }}</h2>
        @for (t of w.tasks; track t.id) {
          <mat-checkbox class="task" [checked]="t.isDone" [disabled]="closed() || busy()" (change)="run(workOrders.toggleTask(w.id, t.id))">
            <span [class.done]="t.isDone">{{ t.description }}</span>
          </mat-checkbox>
        } @empty {
          <p class="muted">No checklist for this job.</p>
        }
      </section>

      <section class="block">
        <h2>Parts used</h2>
        <app-work-order-parts [workOrderId]="w.id" [canAdd]="w.status === 'InProgress' || w.status === 'OnHold'" />
      </section>

      <section class="block">
        <h2>Photos</h2>
        <div class="photos">
          @for (p of photos(); track p.id) {
            <figure>
              <app-secure-image [attachmentId]="p.id" [alt]="p.fileName" />
              <figcaption>
                <span class="muted small">{{ p.uploadedAt | dubaiTime }}</span>
                @if (p.canDelete) {
                  <button mat-icon-button aria-label="Delete photo" (click)="deletePhoto(p.id)"><mat-icon>delete</mat-icon></button>
                }
              </figcaption>
            </figure>
          }
        </div>
        @if (photos().length === 0) {
          <p class="muted">No photos yet.</p>
        }
        @if (canAddPhotos()) {
          <label mat-stroked-button class="upload" [class.disabled]="uploading()">
            <mat-icon>photo_camera</mat-icon>{{ uploading() ? 'Uploading…' : 'Add photo' }}
            <input type="file" accept="image/jpeg,image/png,image/webp" capture="environment" hidden [disabled]="uploading()" (change)="addPhoto($event)" />
          </label>
        }
      </section>

      <section class="block">
        <h2>Time log</h2>
        @for (e of timeEntries.value() ?? []; track e.id) {
          <div class="entry">
            @if (editing() === e.id) {
              <label>Start <input type="datetime-local" [(ngModel)]="editStart" /></label>
              <label>End <input type="datetime-local" [(ngModel)]="editEnd" /></label>
              <div>
                <button mat-button (click)="saveEntry(e)">Save</button>
                <button mat-button (click)="editing.set(null)">Cancel</button>
              </div>
            } @else {
              <div>
                <strong>{{ e.type }}</strong> {{ time(e.startedAt) }}–{{ e.endedAt ? time(e.endedAt) : 'now' }}
                <span class="muted">· {{ minutes(e.durationMinutes) }}</span>
              </div>
              @if (e.canEdit) {
                <button mat-button (click)="editEntry(e)">Correct</button>
              }
            }
          </div>
        } @empty {
          <p class="muted">Time is logged when you tap On my way or Start.</p>
        }
      </section>

      <section class="block">
        <h2>Notes</h2>
        @for (n of notes.value() ?? []; track n.id) {
          <div class="note-item">
            <strong>{{ n.authorName }}</strong> <span class="muted small">{{ n.createdAt | dubaiTime }}</span>
            <p class="pre">{{ n.body }}</p>
          </div>
        }
        <form class="note-form" (ngSubmit)="addNote(w)">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Add a note</mat-label>
            <textarea matInput name="note" [(ngModel)]="newNote" rows="2"></textarea>
          </mat-form-field>
          <button mat-stroked-button type="submit" [disabled]="!newNote().trim()">Add note</button>
        </form>
      </section>

      @if (w.completionNotes) {
        <section class="block">
          <h2>Completion</h2>
          <p class="pre">{{ w.completionNotes }}</p>
          <div class="muted">Signed by {{ w.signedByName }} · {{ w.completedAt | dubaiTime }}</div>
        </section>
      }
    } @else if (loaded.error()) {
      <p class="error">{{ errorMessage() }}</p>
    }
  `,
  styles: `
    :host { display: block; max-width: 720px; margin: 0 auto; }
    .back { display: inline-block; margin-bottom: 8px; }
    h1 { margin: 4px 0 8px; font: var(--mat-sys-headline-small); }
    h2 { font: var(--mat-sys-title-medium); margin: 0 0 8px; }
    .top { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; margin: 16px 0; }
    .actions button { flex: 1 1 140px; min-height: 48px; }
    .block { padding: 16px 0; border-top: 1px solid var(--mat-sys-outline-variant); display: flex; flex-direction: column; gap: 6px; align-items: flex-start; }
    .block a[mat-stroked-button] { margin-top: 4px; }
    .note { display: flex; gap: 6px; align-items: flex-start; background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container);
      padding: 8px; border-radius: 8px; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .small { font: var(--mat-sys-body-small); }
    .pre { white-space: pre-wrap; margin: 0; }
    .task { display: block; min-height: 40px; }
    .done { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .photos { display: grid; grid-template-columns: repeat(auto-fill, minmax(96px, 1fr)); gap: 8px; width: 100%; }
    figure { margin: 0; }
    app-secure-image { aspect-ratio: 1; }
    figcaption { display: flex; justify-content: space-between; align-items: center; }
    .upload { display: inline-flex; align-items: center; gap: 6px; padding: 8px 16px; border: 1px solid var(--mat-sys-outline); border-radius: 20px;
      cursor: pointer; min-height: 40px; }
    .upload.disabled { opacity: 0.6; pointer-events: none; }
    .entry { width: 100%; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; }
    .entry label { display: flex; flex-direction: column; font: var(--mat-sys-body-small); }
    .entry input { font: inherit; padding: 6px; }
    .note-item { width: 100%; }
    .note-form { width: 100%; display: flex; flex-direction: column; gap: 8px; align-items: flex-end; }
    .note-form mat-form-field { width: 100%; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class TechJob {
  private readonly fieldApi = inject(FieldApi);
  protected readonly workOrders = inject(WorkOrdersApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  readonly id = input.required<string>();

  protected readonly loaded = rxResource({ params: () => this.id(), stream: ({ params }) => this.workOrders.get(params) });
  protected readonly workOrder = linkedSignal<WorkOrder | null>(() => this.loaded.value() ?? null);
  protected readonly attachments = rxResource({ params: () => this.id(), stream: ({ params }) => this.fieldApi.attachments(params) });
  protected readonly timeEntries = rxResource({ params: () => this.id(), stream: ({ params }) => this.fieldApi.timeEntries(params) });
  protected readonly notes = rxResource({ params: () => this.id(), stream: ({ params }) => this.workOrders.notes(params) });
  constructor() {
    onWorkOrderChange(() => this.loaded.reload(), (change) => change.id === this.id());
  }

  protected readonly busy = signal(false);
  protected readonly locating = signal(false);
  protected readonly uploading = signal(false);
  protected readonly editing = signal<string | null>(null);
  protected readonly editStart = signal('');
  protected readonly editEnd = signal('');
  protected readonly newNote = signal('');

  protected readonly photos = computed(() => (this.attachments.value() ?? []).filter((a) => a.kind === 'Photo'));
  protected readonly doneCount = computed(() => this.workOrder()?.tasks.filter((t) => t.isDone).length ?? 0);
  protected readonly closed = computed(() => {
    const status = this.workOrder()?.status;
    return status === 'Completed' || status === 'Invoiced' || status === 'Cancelled';
  });
  protected readonly canAddPhotos = computed(() => PHOTO_STATUSES.includes(this.workOrder()?.status ?? 'New'));
  protected readonly errorMessage = computed(() => problemMessage(this.loaded.error()));
  protected readonly time = clockTime;
  protected readonly minutes = formatMinutes;
  protected readonly directions = (w: WorkOrder) => directionsUrl(w.site);

  protected assetLine(a: NonNullable<WorkOrder['asset']>): string {
    return [a.manufacturer, a.model, a.serialNumber].filter((x) => !!x).join(' · ');
  }

  protected assetHistory(w: WorkOrder): void {
    this.dialog.open(AssetHistoryDialog, { data: w.asset, width: '100%', maxWidth: '560px' });
  }

  protected async enRoute(w: WorkOrder): Promise<void> {
    this.busy.set(true);
    this.run(this.fieldApi.enRoute(w.id, await this.location()), 'The office can see you are on your way.');
  }

  protected async start(w: WorkOrder): Promise<void> {
    this.busy.set(true);
    this.run(this.fieldApi.start(w.id, await this.location()), 'Job started.');
  }

  protected hold(w: WorkOrder): void {
    this.dialog
      .open<ReasonDialog, ReasonDialogData, string>(ReasonDialog, {
        data: { title: 'Put on hold', label: 'Why? For example, waiting for a part', confirm: 'Put on hold' },
        width: '100%',
        maxWidth: '480px',
      })
      .afterClosed()
      .pipe(filter((note): note is string => !!note))
      .subscribe((note) => this.run(this.workOrders.hold(w.id, note), 'Job is on hold.'));
  }

  protected complete(w: WorkOrder): void {
    this.dialog
      .open<CompleteDialog, WorkOrder, WorkOrder>(CompleteDialog, { data: w, width: '100%', maxWidth: '560px' })
      .afterClosed()
      .subscribe((done) => {
        if (!done) return;
        this.workOrder.set(done);
        this.refreshSide();
        this.snackBar.open('Job completed.', 'Close', { duration: 3000 });
      });
  }

  /** Runs a call that returns the updated work order. */
  protected run(request: Observable<WorkOrder>, done?: string): void {
    this.busy.set(true);
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: (wo) => {
        this.workOrder.set(wo);
        this.refreshSide();
        if (done) this.snackBar.open(done, 'Close', { duration: 3000 });
      },
      error: (err: unknown) => {
        this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 });
        this.loaded.reload();
      },
    });
  }

  protected async addPhoto(event: Event): Promise<void> {
    const inputEl = event.target as HTMLInputElement;
    const file = inputEl.files?.[0];
    inputEl.value = '';
    const w = this.workOrder();
    if (!file || !w) return;
    if (!PHOTO_TYPES.includes(file.type)) {
      this.snackBar.open('Choose a JPEG, PNG or WebP photo.', 'Close', { duration: 5000 });
      return;
    }
    this.uploading.set(true);
    const photo = await compressPhoto(file);
    if (photo.size > MAX_UPLOAD_BYTES) {
      this.uploading.set(false);
      this.snackBar.open('The photo is larger than 10 MB.', 'Close', { duration: 5000 });
      return;
    }
    const name = photo === file ? file.name : jpegName(file.name);
    this.fieldApi
      .upload(w.id, 'Photo', photo, name)
      .pipe(finalize(() => this.uploading.set(false)))
      .subscribe({
        next: () => this.attachments.reload(),
        error: (err: unknown) => this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 }),
      });
  }

  protected deletePhoto(id: string): void {
    this.fieldApi.deleteAttachment(id).subscribe({
      next: () => this.attachments.reload(),
      error: (err: unknown) => this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 }),
    });
  }

  protected editEntry(e: TimeEntry): void {
    this.editStart.set(utcToDubaiLocal(e.startedAt));
    this.editEnd.set(e.endedAt ? utcToDubaiLocal(e.endedAt) : '');
    this.editing.set(e.id);
  }

  protected saveEntry(e: TimeEntry): void {
    if (!this.editStart()) return;
    const end = this.editEnd() ? dubaiLocalToUtc(this.editEnd()) : null;
    this.fieldApi.correctTimeEntry(e.id, dubaiLocalToUtc(this.editStart()), end).subscribe({
      next: () => {
        this.editing.set(null);
        this.timeEntries.reload();
      },
      error: (err: unknown) => this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 }),
    });
  }

  protected addNote(w: WorkOrder): void {
    this.workOrders.addNote(w.id, this.newNote().trim(), false).subscribe({
      next: () => {
        this.newNote.set('');
        this.notes.reload();
      },
      error: (err: unknown) => this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 }),
    });
  }

  private refreshSide(): void {
    this.timeEntries.reload();
    this.attachments.reload();
  }

  private async location(): Promise<Location | null> {
    this.locating.set(true);
    try {
      return await currentLocation();
    } finally {
      this.locating.set(false);
    }
  }
}
