import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { WorkOrderPriority, WorkOrderStatus } from './work-orders.api';

const STATUS_LABELS: Record<WorkOrderStatus, string> = {
  New: 'New',
  Scheduled: 'Scheduled',
  Dispatched: 'Dispatched',
  EnRoute: 'En route',
  InProgress: 'In progress',
  OnHold: 'On hold',
  Completed: 'Completed',
  Invoiced: 'Invoiced',
  Cancelled: 'Cancelled',
};

export function statusLabel(status: WorkOrderStatus): string {
  return STATUS_LABELS[status] ?? status;
}

/** Moves one item of a list up (-1) or down (+1), returning a new list; out-of-range moves return the list unchanged. */
export function move<T>(items: readonly T[], index: number, delta: -1 | 1): T[] {
  const target = index + delta;
  if (target < 0 || target >= items.length) return [...items];
  const copy = [...items];
  [copy[index], copy[target]] = [copy[target], copy[index]];
  return copy;
}

@Component({
  selector: 'app-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="chip" [class]="'chip ' + status().toLowerCase()">{{ label() }}</span>`,
  styles: `
    .chip { display: inline-block; padding: 2px 10px; border-radius: 12px; font: var(--mat-sys-label-medium);
      background: var(--mat-sys-surface-container-high); color: var(--mat-sys-on-surface); white-space: nowrap; }
    .scheduled, .dispatched, .enroute { background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
    .inprogress { background: var(--mat-sys-primary-container); color: var(--mat-sys-on-primary-container); }
    .onhold { background: var(--mat-sys-tertiary-container); color: var(--mat-sys-on-tertiary-container); }
    .completed, .invoiced { background: #d7f0dc; color: #0f5223; }
    .cancelled { background: var(--mat-sys-error-container); color: var(--mat-sys-on-error-container); }
  `,
})
export class StatusChip {
  readonly status = input.required<WorkOrderStatus>();
  protected readonly label = computed(() => statusLabel(this.status()));
}

@Component({
  selector: 'app-priority-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span [class]="'priority ' + priority().toLowerCase()">{{ priority() }}</span>`,
  styles: `
    .priority { font: var(--mat-sys-label-medium); text-transform: uppercase; letter-spacing: 0.04em; }
    .urgent { color: var(--mat-sys-error); font-weight: 700; }
    .high { color: #b45309; font-weight: 600; }
    .medium { color: var(--mat-sys-on-surface); }
    .low { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class PriorityChip {
  readonly priority = input.required<WorkOrderPriority>();
}
