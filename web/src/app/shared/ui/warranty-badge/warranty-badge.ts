import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Formats an ISO date (yyyy-mm-dd) as e.g. "1 Oct 2026" without shifting it through a time zone. */
export function formatDate(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  });
}

@Component({
  selector: 'app-warranty-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (underWarranty() && expiresOn()) {
      <span class="badge">Under warranty until {{ label() }}</span>
    }
  `,
  styles: `
    .badge {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 12px;
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
  `,
})
export class WarrantyBadge {
  readonly expiresOn = input<string | null>(null);
  readonly underWarranty = input(false);
  protected readonly label = computed(() => formatDate(this.expiresOn()!));
}
