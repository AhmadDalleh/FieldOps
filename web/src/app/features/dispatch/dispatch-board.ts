import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDragPlaceholder, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { problemMessage } from '../../core/problem';
import { dubaiToday, formatDubai } from '../../shared/time/dubai-time';
import { DubaiTimePipe } from '../../shared/time/dubai-time.pipe';
import { statusLabel } from '../work-orders/work-order-labels';
import { ScheduleFlow } from '../work-orders/schedule-flow';
import { WorkOrdersApi } from '../work-orders/work-orders.api';
import {
  BOARD_HOURS, BOARD_MINUTES, PRIORITY_COLORS, addDays, blockGeometry, boardMinute, isoAtBoardMinute, pixelsToMinutes,
  placeJob, resizedEnd, snap, workingHoursGeometry,
} from './board-layout';
import { BoardJob, BoardTechnician, DispatchApi } from './dispatch.api';
import { onWorkOrderChange } from '../../core/realtime';

/** Statuses a dispatcher may still move on the board (the Schedule action). */
const MOVABLE = new Set(['New', 'Scheduled', 'Dispatched']);
const UNASSIGNABLE = new Set(['Scheduled', 'Dispatched']);

interface Resize {
  jobId: string;
  pointerX: number;
  rowWidth: number;
  start: number;
  end: number;
  newEnd: number;
}

/** US-DSP-02: technicians as rows, 07:00–20:00 as columns, unassigned jobs on the left. */
@Component({
  selector: 'app-dispatch-board',
  imports: [
    CdkDropListGroup, CdkDropList, CdkDrag, CdkDragHandle, CdkDragPlaceholder, RouterLink, MatButtonModule, MatIconModule,
    DubaiTimePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Dispatch board</h1>
      <div class="nav">
        <button mat-icon-button aria-label="Previous day" (click)="shift(-1)"><mat-icon>chevron_left</mat-icon></button>
        <input class="date" type="date" aria-label="Board date" [value]="date()" (change)="pick($any($event.target).value)" />
        <button mat-icon-button aria-label="Next day" (click)="shift(1)"><mat-icon>chevron_right</mat-icon></button>
        <button mat-button (click)="pick(today())" [disabled]="date() === today()">Today</button>
        <button mat-stroked-button (click)="board.reload()"><mat-icon>refresh</mat-icon>Refresh</button>
        <a mat-button routerLink="/office/dispatch/map" [queryParams]="{ date: date() }"><mat-icon>map</mat-icon>Map</a>
      </div>
    </header>
    <div class="legend">
      @for (p of priorities; track p) {
        <span><i class="dot" [style.background]="colors[p]"></i>{{ p }}</span>
      }
      <span><i class="dot striped"></i>Scheduled</span>
      <span><i class="dot"></i>Dispatched</span>
      <span><i class="dot grey"></i>Time off</span>
    </div>

    @if (board.error()) {
      <p class="error">{{ errorMessage() }}</p>
    }

    <div class="layout" cdkDropListGroup>
      <section class="panel" aria-label="Unassigned work orders">
        <h2>Unassigned ({{ unassigned().length }})</h2>
        <div
          class="panel-list"
          cdkDropList
          id="unassigned"
          [cdkDropListData]="null"
          [cdkDropListSortingDisabled]="true"
          [cdkDropListEnterPredicate]="canUnassign"
          (cdkDropListDropped)="dropped($any($event))"
        >
          @for (j of unassigned(); track j.id) {
            <div class="card" cdkDrag [cdkDragData]="j" [style.border-left-color]="colors[j.priority]" (cdkDragStarted)="dragging = true" (cdkDragEnded)="dragEnded()">
              <div class="card-head"><a [routerLink]="['/office/work-orders', j.id]" (click)="$event.stopPropagation()">{{ j.number }}</a> <span class="prio" [style.color]="colors[j.priority]">{{ j.priority }}</span></div>
              <div class="title">{{ j.title }}</div>
              <div class="muted">{{ j.customerName }} · {{ j.siteName }}</div>
              @if (j.dueBy) {
                <div class="muted">Due {{ j.dueBy | dubaiTime }}</div>
              }
            </div>
          } @empty {
            <p class="muted">Nothing waiting. Drop a job here to unassign it.</p>
          }
        </div>
      </section>

      <section class="grid" aria-label="Technician schedule">
        <div class="hours">
          <div class="name-col"></div>
          <div class="hour-track">
            @for (h of hours; track h) {
              <span class="hour" [style.left.%]="((h - hours[0]) * 60 / boardMinutes) * 100">{{ h.toString().padStart(2, '0') }}:00</span>
            }
          </div>
        </div>
        @for (t of technicians(); track t.id) {
          <div class="row">
            <div class="name-col">
              <div><i class="dot" [style.background]="t.color"></i><strong>{{ t.name }}</strong></div>
              <div class="muted small">{{ skillNames(t) }}</div>
              <button mat-button class="dispatch" [disabled]="!hasScheduled(t.id)" (click)="dispatchDay(t)">Dispatch day</button>
            </div>
            <div
              class="track"
              cdkDropList
              [id]="'row-' + t.id"
              [cdkDropListData]="t.id"
              [cdkDropListSortingDisabled]="true"
              [attr.data-technician]="t.name"
              (cdkDropListDropped)="dropped($any($event))"
            >
              <div class="working" [style.left.%]="working(t).left" [style.width.%]="working(t).width"></div>
              @for (h of hours; track h) {
                <i class="gridline" [style.left.%]="((h - hours[0]) * 60 / boardMinutes) * 100"></i>
              }
              @for (o of timeOffFor(t.id); track o.id) {
                <div class="time-off" [style.left.%]="o.left" [style.width.%]="o.width" [title]="o.reason || 'Time off'">Time off</div>
              }
              @for (b of blocksFor(t.id); track b.job.id) {
                <div
                  class="block"
                  [class]="'block s-' + b.job.status.toLowerCase()"
                  cdkDrag
                  [cdkDragData]="b.job"
                  [cdkDragDisabled]="!movable(b.job)"
                  [style.left.%]="b.left"
                  [style.width.%]="b.width"
                  [style.background-color]="colors[b.job.priority]"
                  [title]="tooltip(b.job)"
                  (cdkDragStarted)="dragging = true"
                  (cdkDragEnded)="dragEnded()"
                >
                  <div class="block-body" cdkDragHandle (click)="open(b.job)">
                    <strong>{{ b.job.number }}</strong> {{ b.job.title }}
                    <div class="block-sub">{{ b.job.customerName }} · {{ label(b.job.status) }}</div>
                  </div>
                  @if (movable(b.job)) {
                    <div class="resize" aria-hidden="true" (pointerdown)="startResize($event, b.job)" (pointermove)="moveResize($event)" (pointerup)="endResize($event)" (pointercancel)="resize.set(null)"></div>
                  }
                  <div *cdkDragPlaceholder class="placeholder"></div>
                </div>
              }
            </div>
          </div>
        } @empty {
          @if (board.hasValue()) {
            <p class="muted">No active technicians.</p>
          }
        }
      </section>
    </div>
  `,
  styles: `
    .header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; }
    .header h1 { margin: 0; }
    .nav { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; }
    .date { font: inherit; padding: 6px 8px; border: 1px solid var(--mat-sys-outline-variant); border-radius: 6px; background: transparent; color: inherit; }
    .legend { display: flex; gap: 16px; flex-wrap: wrap; margin: 8px 0 16px; font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
    .dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 6px; background: #455a64; vertical-align: middle; }
    .dot.striped { background: repeating-linear-gradient(45deg, #455a64 0 2px, #b0bec5 2px 4px); }
    .dot.grey { background: #bdbdbd; border-radius: 2px; }
    .layout { display: grid; grid-template-columns: 260px 1fr; gap: 16px; align-items: start; }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
    .panel h2 { font: var(--mat-sys-title-small); margin: 0 0 8px; }
    .panel-list { display: flex; flex-direction: column; gap: 8px; min-height: 120px; max-height: calc(100vh - 220px); overflow: auto;
      padding: 4px; border-radius: 8px; background: var(--mat-sys-surface-container-low); }
    .card { background: var(--mat-sys-surface); border: 1px solid var(--mat-sys-outline-variant); border-left: 4px solid; border-radius: 6px;
      padding: 8px; cursor: grab; font: var(--mat-sys-body-small); }
    .card-head { display: flex; justify-content: space-between; }
    .prio { font-weight: 600; text-transform: uppercase; }
    .title { font: var(--mat-sys-body-medium); margin: 2px 0; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .small { font: var(--mat-sys-body-small); }
    .grid { overflow-x: auto; }
    .hours, .row { display: grid; grid-template-columns: 170px minmax(720px, 1fr); }
    .hour-track { position: relative; height: 20px; }
    .hour { position: absolute; transform: translateX(-50%); font: var(--mat-sys-label-small); color: var(--mat-sys-on-surface-variant); }
    .hour:first-child { transform: none; }
    .row { border-top: 1px solid var(--mat-sys-outline-variant); }
    .name-col { padding: 6px 8px 6px 0; }
    .dispatch { margin-left: -12px; }
    .track { position: relative; height: 72px; background: var(--mat-sys-surface-container-high); }
    .gridline { position: absolute; top: 0; bottom: 0; border-left: 1px solid var(--mat-sys-outline-variant); opacity: 0.6; }
    .working { position: absolute; top: 0; bottom: 0; background: var(--mat-sys-surface); }
    .time-off { position: absolute; top: 4px; bottom: 4px; background: repeating-linear-gradient(45deg, #bdbdbd 0 6px, #d6d6d6 6px 12px);
      border-radius: 4px; color: #424242; font: var(--mat-sys-label-small); padding: 4px; overflow: hidden; }
    .block { position: absolute; top: 6px; bottom: 6px; border-radius: 6px; color: #fff; overflow: hidden; display: flex;
      box-shadow: 0 1px 2px rgba(0,0,0,.3); font: var(--mat-sys-label-small); min-width: 8px; box-sizing: border-box; }
    .block-body { flex: 1; padding: 4px 6px; cursor: grab; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
    .block-sub { opacity: 0.9; overflow: hidden; text-overflow: ellipsis; }
    .resize { width: 8px; cursor: ew-resize; background: rgba(255,255,255,.35); touch-action: none; }
    .s-scheduled { background-image: repeating-linear-gradient(45deg, rgba(255,255,255,.22) 0 6px, transparent 6px 12px); }
    .s-enroute, .s-inprogress { outline: 3px solid #1b5e20; outline-offset: -3px; }
    .s-onhold { outline: 3px dashed #f9a825; outline-offset: -3px; }
    .s-completed, .s-invoiced { opacity: 0.5; }
    .placeholder { display: none; }
    .cdk-drag-preview { box-shadow: 0 4px 12px rgba(0,0,0,.35); }
    .cdk-drop-list-receiving.track, .cdk-drop-list-dragging.track { outline: 2px dashed var(--mat-sys-primary); outline-offset: -2px; }
    .error { color: var(--mat-sys-error); }
  `,
})
export class DispatchBoard {
  private readonly dispatchApi = inject(DispatchApi);
  private readonly api = inject(WorkOrdersApi);
  private readonly flow = inject(ScheduleFlow);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  protected readonly colors = PRIORITY_COLORS;
  protected readonly priorities = ['Urgent', 'High', 'Medium', 'Low'];
  protected readonly hours = BOARD_HOURS;
  protected readonly boardMinutes = BOARD_MINUTES;
  protected readonly label = statusLabel;
  protected readonly today = signal(dubaiToday());
  protected readonly date = signal(this.today());
  protected readonly resize = signal<Resize | null>(null);
  /** Set while dragging so the click that ends a drag does not open the job. */
  protected dragging = false;

  protected readonly board = rxResource({ params: () => this.date(), stream: ({ params }) => this.dispatchApi.board(params) });
  constructor() {
    onWorkOrderChange(() => this.board.reload());
  }
  protected readonly technicians = computed(() => this.board.value()?.technicians ?? []);
  protected readonly unassigned = computed(() => this.board.value()?.unassigned ?? []);
  protected readonly errorMessage = computed(() => problemMessage(this.board.error()));

  private readonly blocks = computed(() => {
    const board = this.board.value();
    const resize = this.resize();
    const byTech = new Map<string, { job: BoardJob; left: number; width: number }[]>();
    if (!board) return byTech;
    for (const job of board.jobs) {
      if (!job.technicianId || !job.scheduledStart || !job.scheduledEnd) continue;
      const end = resize?.jobId === job.id ? isoAtBoardMinute(board.date, resize.newEnd) : job.scheduledEnd;
      const g = blockGeometry(job.scheduledStart, end, board.date);
      if (!g) continue;
      const list = byTech.get(job.technicianId) ?? [];
      list.push({ job, ...g });
      byTech.set(job.technicianId, list);
    }
    return byTech;
  });

  private readonly timeOff = computed(() => {
    const board = this.board.value();
    const byTech = new Map<string, { id: string; reason: string | null; left: number; width: number }[]>();
    if (!board) return byTech;
    for (const t of board.timeOff) {
      const g = blockGeometry(t.startsAt, t.endsAt, board.date);
      if (!g) continue;
      const list = byTech.get(t.technicianId) ?? [];
      list.push({ id: t.id, reason: t.reason, ...g });
      byTech.set(t.technicianId, list);
    }
    return byTech;
  });

  protected readonly canUnassign = (drag: CdkDrag<BoardJob>) => UNASSIGNABLE.has(drag.data.status);

  protected blocksFor(technicianId: string) {
    return this.blocks().get(technicianId) ?? [];
  }

  protected timeOffFor(technicianId: string) {
    return this.timeOff().get(technicianId) ?? [];
  }

  protected working(t: BoardTechnician) {
    return workingHoursGeometry(t.workingHoursStart, t.workingHoursEnd);
  }

  protected skillNames(t: BoardTechnician): string {
    return t.skills.map((s) => s.name).join(', ') || 'No skills';
  }

  protected movable(job: BoardJob): boolean {
    return MOVABLE.has(job.status);
  }

  protected hasScheduled(technicianId: string): boolean {
    return this.blocksFor(technicianId).some((b) => b.job.status === 'Scheduled');
  }

  protected tooltip(job: BoardJob): string {
    const time = job.scheduledStart && job.scheduledEnd ? `${formatDubai(job.scheduledStart)} to ${formatDubai(job.scheduledEnd).slice(-5)}` : '';
    return `${job.number} · ${job.title}\n${job.customerName}, ${job.siteAddress}\n${time} · ${statusLabel(job.status)} · ${job.priority}`;
  }

  protected shift(days: number): void {
    this.date.set(addDays(this.date(), days));
  }

  protected pick(date: string): void {
    if (date) this.date.set(date);
  }

  protected dragEnded(): void {
    setTimeout(() => (this.dragging = false));
  }

  protected open(job: BoardJob): void {
    if (!this.dragging && !this.resize()) this.router.navigate(['/office/work-orders', job.id]);
  }

  /** Handles every drop: panel → row schedules, row → row moves, row → panel unassigns. */
  protected dropped(event: CdkDragDrop<string | null, string | null, BoardJob>): void {
    const job = event.item.data;
    const technicianId = event.container.data;
    const date = this.date();

    if (technicianId === null) {
      if (event.previousContainer.data !== null) this.act(this.api.unassign(job.id), `${job.number} unassigned.`);
      return;
    }

    const rect = event.container.element.nativeElement.getBoundingClientRect();
    let slot: { start: number; end: number };
    if (job.scheduledStart && job.scheduledEnd && event.previousContainer.data !== null) {
      // An existing block keeps its length and moves by the distance dragged.
      const start = boardMinute(job.scheduledStart, date);
      const duration = boardMinute(job.scheduledEnd, date) - start;
      slot = placeJob(start + snap(pixelsToMinutes(event.distance.x, rect.width)), duration);
      if (technicianId === job.technicianId && isoAtBoardMinute(date, slot.start) === new Date(job.scheduledStart).toISOString()) return;
    } else {
      slot = placeJob(pixelsToMinutes(event.dropPoint.x - rect.left, rect.width));
    }
    this.schedule(job, technicianId, slot.start, slot.end);
  }

  protected startResize(event: PointerEvent, job: BoardJob): void {
    event.stopPropagation();
    event.preventDefault();
    const track = (event.target as HTMLElement).closest('.track') as HTMLElement;
    (event.target as HTMLElement).setPointerCapture?.(event.pointerId);
    const start = boardMinute(job.scheduledStart!, this.date());
    const end = boardMinute(job.scheduledEnd!, this.date());
    this.resize.set({ jobId: job.id, pointerX: event.clientX, rowWidth: track.getBoundingClientRect().width, start, end, newEnd: end });
  }

  protected moveResize(event: PointerEvent): void {
    const r = this.resize();
    if (!r) return;
    this.resize.set({ ...r, newEnd: resizedEnd(r.start, r.end, pixelsToMinutes(event.clientX - r.pointerX, r.rowWidth)) });
  }

  protected endResize(event: PointerEvent): void {
    const r = this.resize();
    if (!r) return;
    event.stopPropagation();
    const job = this.board.value()?.jobs.find((j) => j.id === r.jobId);
    if (!job || r.newEnd === r.end) {
      setTimeout(() => this.resize.set(null));
      return;
    }
    this.schedule(job, job.technicianId!, r.start, r.newEnd, () => this.resize.set(null));
  }

  protected dispatchDay(t: BoardTechnician): void {
    this.api.dispatchDay(t.id, this.date()).subscribe({
      next: (r) => {
        this.snackBar.open(`Dispatched ${r.dispatched.length} job(s) to ${t.name}.`, 'Close', { duration: 4000 });
        this.board.reload();
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  private schedule(job: BoardJob, technicianId: string, start: number, end: number, done?: () => void): void {
    const date = this.date();
    this.flow
      .schedule(job.id, { technicianId, start: isoAtBoardMinute(date, start), end: isoAtBoardMinute(date, end) })
      .subscribe({
        next: () => this.board.reload(),
        error: (err: unknown) => {
          done?.();
          this.fail(err);
        },
        complete: () => {
          done?.();
          this.board.reload();
        },
      });
  }

  private act(request: Observable<unknown>, message: string): void {
    request.subscribe({
      next: () => {
        this.snackBar.open(message, 'Close', { duration: 3000 });
        this.board.reload();
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  private fail(err: unknown): void {
    this.snackBar.open(problemMessage(err), 'Close', { duration: 6000 });
    this.board.reload();
  }
}
