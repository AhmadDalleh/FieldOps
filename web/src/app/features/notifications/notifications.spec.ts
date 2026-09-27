import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { Realtime } from '../../core/realtime';
import { AppNotification, Notifications, notificationIcon, timeAgo } from './notifications';

const now = Date.UTC(2026, 9, 1, 6, 0, 0);
const minutesAgo = (m: number) => new Date(now - m * 60_000).toISOString();

function notification(id: string, isRead = false): AppNotification {
  return { id, type: 'JobAssigned', title: `Job ${id}`, body: null, link: `/tech/jobs/${id}`, isRead, createdAt: minutesAgo(1) };
}

describe('timeAgo', () => {
  it('reads as minutes, hours, then a Dubai date', () => {
    expect(timeAgo(minutesAgo(0), now)).toBe('just now');
    expect(timeAgo(minutesAgo(5), now)).toBe('5 min ago');
    expect(timeAgo(minutesAgo(3 * 60 + 10), now)).toBe('3 h ago');
    expect(timeAgo('2026-09-28T21:00:00Z', now)).toBe('29 Sept');
  });
});

describe('notificationIcon', () => {
  it('has an icon per type and a fallback', () => {
    expect(notificationIcon('LowStock')).toBe('inventory_2');
    expect(notificationIcon('Unknown' as never)).toBe('notifications');
  });
});

describe('Notifications', () => {
  let http: HttpTestingController;
  let pushed: Subject<unknown>;
  const user = signal<{ id: string } | null>({ id: 'u1' });

  beforeEach(() => {
    pushed = new Subject();
    user.set({ id: 'u1' });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { user } },
        { provide: Realtime, useValue: { notifications: pushed.asObservable() } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function loaded(): Notifications {
    const store = TestBed.inject(Notifications);
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/notifications').flush({ items: [notification('a'), notification('b', true)], unreadCount: 1 });
    return store;
  }

  it('loads for the signed-in user and adds live ones on top', () => {
    const store = loaded();
    expect(store.unreadCount()).toBe(1);

    pushed.next(notification('c'));
    expect(store.items().map((n) => n.id)).toEqual(['c', 'a', 'b']);
    expect(store.unreadCount()).toBe(2);
  });

  it('marks one read once, and all read, straight away', () => {
    const store = loaded();
    const first = store.items()[0];

    store.markRead(first);
    http.expectOne({ method: 'POST', url: '/api/notifications/a/read' }).flush(null);
    expect(store.unreadCount()).toBe(0);
    store.markRead(store.items()[0]);
    http.expectNone('/api/notifications/a/read');

    pushed.next(notification('c'));
    store.markAllRead();
    http.expectOne({ method: 'POST', url: '/api/notifications/read-all' }).flush(null);
    expect(store.unreadCount()).toBe(0);
    expect(store.items().every((n) => n.isRead)).toBe(true);
  });

  it('forgets everything on sign-out', () => {
    const store = loaded();
    user.set(null);
    TestBed.tick();
    expect(store.items()).toEqual([]);
    expect(store.unreadCount()).toBe(0);
    http.verify();
  });
});
