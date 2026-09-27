import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { AppNotification, Notifications, notificationIcon, timeAgo } from './notifications';

/** US-NOT-01: the bell with the unread count, opening the latest notifications. */
@Component({
  selector: 'app-notification-bell',
  imports: [MatButtonModule, MatIconModule, MatBadgeModule, MatMenuModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button mat-icon-button [matMenuTriggerFor]="menu" (menuOpened)="refresh()"
      [attr.aria-label]="store.unreadCount() ? store.unreadCount() + ' unread notifications' : 'Notifications'">
      <mat-icon [matBadge]="store.unreadCount() || null" matBadgeColor="warn" matBadgeSize="small" aria-hidden="false">notifications</mat-icon>
    </button>
    <mat-menu #menu="matMenu" xPosition="before" class="notification-menu">
      <div class="head" (click)="$event.stopPropagation()">
        <strong>Notifications</strong>
        @if (store.unreadCount()) {
          <button mat-button (click)="store.markAllRead()">Mark all read</button>
        }
      </div>
      @for (n of store.items(); track n.id) {
        <button mat-menu-item class="item" [class.unread]="!n.isRead" (click)="open(n)">
          <mat-icon>{{ icon(n.type) }}</mat-icon>
          <span class="text">
            <span class="title">{{ n.title }}</span>
            @if (n.body) {
              <span class="body">{{ n.body }}</span>
            }
            <span class="when">{{ ago(n.createdAt) }}</span>
          </span>
        </button>
      } @empty {
        <p class="empty">You're all caught up.</p>
      }
    </mat-menu>
  `,
  styles: `
    .head { display: flex; justify-content: space-between; align-items: center; gap: 16px; padding: 4px 8px 4px 16px; min-width: 340px; }
    .head button { white-space: nowrap; }
    .item { height: auto !important; min-height: 56px; padding-top: 8px; padding-bottom: 8px; max-width: 380px; }
    .item.unread { background: var(--mat-sys-secondary-container); }
    .text { display: flex; flex-direction: column; white-space: normal; line-height: 1.3; }
    .title { font-weight: 600; }
    .body { font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
    .when { font: var(--mat-sys-label-small); color: var(--mat-sys-on-surface-variant); }
    .empty { padding: 8px 16px; margin: 0; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class NotificationBell {
  protected readonly store = inject(Notifications);
  private readonly router = inject(Router);
  protected readonly icon = notificationIcon;
  protected readonly ago = (iso: string) => timeAgo(iso);

  protected refresh(): void {
    this.store.load().subscribe({ error: () => undefined });
  }

  protected open(n: AppNotification): void {
    this.store.markRead(n);
    if (n.link) void this.router.navigateByUrl(n.link);
  }
}
