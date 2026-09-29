import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { Realtime } from '../../core/realtime';

export type NotificationType =
  | 'JobAssigned'
  | 'JobRescheduled'
  | 'JobUnassigned'
  | 'JobCancelled'
  | 'JobOnHold'
  | 'JobCompleted'
  | 'TimeOffRequested'
  | 'TimeOffDecided'
  | 'LowStock';

export interface AppNotification {
  id: string;
  type: NotificationType;
  title: string;
  body: string | null;
  link: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationList {
  items: AppNotification[];
  unreadCount: number;
}

const ICONS: Record<NotificationType, string> = {
  JobAssigned: 'assignment_ind',
  JobRescheduled: 'update',
  JobUnassigned: 'event_busy',
  JobCancelled: 'cancel',
  JobOnHold: 'pause_circle',
  JobCompleted: 'task_alt',
  TimeOffRequested: 'beach_access',
  TimeOffDecided: 'beach_access',
  LowStock: 'inventory_2',
};

export function notificationIcon(type: NotificationType): string {
  return ICONS[type] ?? 'notifications';
}

/** "just now", "5 min ago", "3 h ago", then the date. */
export function timeAgo(iso: string, now = Date.now()): string {
  const minutes = Math.floor((now - new Date(iso).getTime()) / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  if (minutes < 24 * 60) return `${Math.floor(minutes / 60)} h ago`;
  return new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', timeZone: 'Asia/Dubai' });
}

/** US-NOT-01: the signed-in user's notifications, kept current by the live connection. */
@Injectable({ providedIn: 'root' })
export class Notifications {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly state = signal<NotificationList>({ items: [], unreadCount: 0 });

  readonly items = computed(() => this.state().items);
  readonly unreadCount = computed(() => this.state().unreadCount);

  constructor() {
    inject(Realtime).notifications.subscribe((n) => {
      const notification = n as AppNotification;
      this.state.update((s) => ({ items: [notification, ...s.items].slice(0, 50), unreadCount: s.unreadCount + 1 }));
    });
    effect(() => {
      if (this.auth.user()) this.load().subscribe({ error: () => undefined });
      else this.state.set({ items: [], unreadCount: 0 });
    });
  }

  load(): Observable<NotificationList> {
    return this.http
      .get<NotificationList>('/api/notifications', { params: new HttpParams().set('take', 30) })
      .pipe(tap((list) => this.state.set(list)));
  }

  markRead(n: AppNotification): void {
    if (n.isRead) return;
    this.state.update((s) => ({
      items: s.items.map((i) => (i.id === n.id ? { ...i, isRead: true } : i)),
      unreadCount: Math.max(0, s.unreadCount - 1),
    }));
    this.http.post(`/api/notifications/${n.id}/read`, null).subscribe({ error: () => undefined });
  }

  markAllRead(): void {
    this.state.update((s) => ({ items: s.items.map((i) => ({ ...i, isRead: true })), unreadCount: 0 }));
    this.http.post('/api/notifications/read-all', null).subscribe({ error: () => undefined });
  }
}
