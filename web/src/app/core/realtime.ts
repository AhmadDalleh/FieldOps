import { Injectable, OnDestroy, computed, effect, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, debounceTime, filter, firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';

export interface WorkOrderChanged {
  id: string;
  status: string;
}

/** True when the JWT expires within the margin (or cannot be read), so a fresh one is fetched first. */
export function expiresSoon(token: string, now = Date.now(), marginMs = 30_000): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return typeof payload.exp !== 'number' || payload.exp * 1000 - now < marginMs;
  } catch {
    return true;
  }
}

/**
 * The live connection to /hubs/notifications (docs/02-architecture.md), open while someone is signed in.
 * Screens listen to `workOrderChanges()` to refresh; the bell listens to `notifications`.
 */
@Injectable({ providedIn: 'root' })
export class Realtime implements OnDestroy {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;
  private readonly changed = new Subject<WorkOrderChanged>();
  private readonly created = new Subject<unknown>();

  readonly notifications = this.created.asObservable();

  // Only a different user reconnects; a token refresh replaces the session but not the user.
  private readonly userId = computed(() => this.auth.user()?.id ?? null);

  constructor() {
    effect(() => {
      const userId = this.userId();
      void this.stop().then(() => (userId ? this.start() : undefined));
    });
  }

  /** Work order changes, bunched so a burst (a day dispatched at once) causes one refresh. */
  workOrderChanges(predicate: (change: WorkOrderChanged) => boolean = () => true): Observable<WorkOrderChanged> {
    return this.changed.pipe(filter(predicate), debounceTime(300));
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private async start(): Promise<void> {
    let connection: HubConnection;
    try {
      connection = new HubConnectionBuilder()
        .withUrl('/hubs/notifications', { accessTokenFactory: () => this.token() })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();
    } catch {
      return; // no usable URL (e.g. outside a browser): the app works without live updates
    }
    connection.on('WorkOrderChanged', (change: WorkOrderChanged) => this.changed.next(change));
    connection.on('NotificationCreated', (notification: unknown) => this.created.next(notification));
    // Automatic reconnect gives up after a few tries; keep going while signed in.
    connection.onclose(() => this.connection === connection && void this.connect(connection));
    this.connection = connection;
    await this.connect(connection);
  }

  /** Keeps trying while this is still the current connection: offline or a restarting API should not end live updates. */
  private async connect(connection: HubConnection): Promise<void> {
    try {
      await connection.start();
    } catch {
      if (this.connection !== connection) return;
      setTimeout(() => this.connection === connection && void this.connect(connection), 10_000);
    }
  }

  private async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    if (connection && connection.state !== HubConnectionState.Disconnected) await connection.stop();
  }

  private async token(): Promise<string> {
    const token = this.auth.accessToken();
    if (token && !expiresSoon(token)) return token;
    return firstValueFrom(this.auth.refresh());
  }
}

/** Runs `refresh` when a matching work order changes, for as long as the calling component lives (US-DSP-02 AC5). */
export function onWorkOrderChange(refresh: () => void, predicate?: (change: WorkOrderChanged) => boolean): void {
  inject(Realtime).workOrderChanges(predicate).pipe(takeUntilDestroyed()).subscribe(() => refresh());
}
