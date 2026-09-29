import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, afterNextRender, computed, effect, inject, signal, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import * as L from 'leaflet';
import { problemMessage } from '../../core/problem';
import { dubaiToday, formatDubai } from '../../shared/time/dubai-time';
import { statusLabel } from '../work-orders/work-order-labels';
import { STATUS_COLORS, addDays } from './board-layout';
import { BoardJob, DispatchApi } from './dispatch.api';

const DUBAI: L.LatLngTuple = [25.2048, 55.2708];

function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
}

/** US-DSP-05: the day's scheduled jobs as pins coloured by status. */
@Component({
  selector: 'app-dispatch-map',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <h1>Job map</h1>
      <div class="nav">
        <button mat-icon-button aria-label="Previous day" (click)="date.set(shift(-1))"><mat-icon>chevron_left</mat-icon></button>
        <input class="date" type="date" aria-label="Map date" [value]="date()" (change)="pick($any($event.target).value)" />
        <button mat-icon-button aria-label="Next day" (click)="date.set(shift(1))"><mat-icon>chevron_right</mat-icon></button>
        <button mat-stroked-button (click)="board.reload()"><mat-icon>refresh</mat-icon>Refresh</button>
        <a mat-button routerLink="/office/dispatch"><mat-icon>view_timeline</mat-icon>Board</a>
      </div>
    </header>
    <div class="legend">
      @for (s of legend; track s.status) {
        <span><i class="dot" [style.background]="s.color"></i>{{ s.label }}</span>
      }
    </div>
    <p class="summary">
      @if (board.error()) {
        <span class="error">{{ errorMessage() }}</span>
      } @else if (board.hasValue()) {
        {{ pinned().length }} of {{ jobs().length }} job(s) on the map{{ jobs().length > pinned().length ? '; the rest have sites without a pin.' : '.' }}
      }
    </p>
    <div #map class="map" role="region" aria-label="Map of the day's jobs"></div>
  `,
  styles: `
    .header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; }
    .header h1 { margin: 0; }
    .nav { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; }
    .date { font: inherit; padding: 6px 8px; border: 1px solid var(--mat-sys-outline-variant); border-radius: 6px; background: transparent; color: inherit; }
    .legend { display: flex; gap: 16px; flex-wrap: wrap; margin: 8px 0; font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); }
    .dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 6px; vertical-align: middle; }
    .summary { margin: 0 0 8px; color: var(--mat-sys-on-surface-variant); }
    .map { height: calc(100vh - 230px); min-height: 360px; border-radius: 8px; border: 1px solid var(--mat-sys-outline-variant); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class DispatchMap implements OnDestroy {
  private readonly api = inject(DispatchApi);
  private readonly container = viewChild.required<ElementRef<HTMLElement>>('map');
  private map?: L.Map;
  private readonly layer = L.layerGroup();

  protected readonly date = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('date') ?? dubaiToday());
  protected readonly board = rxResource({ params: () => this.date(), stream: ({ params }) => this.api.board(params) });
  protected readonly jobs = computed(() => this.board.value()?.jobs ?? []);
  protected readonly pinned = computed(() => this.jobs().filter((j) => j.latitude != null && j.longitude != null));
  protected readonly errorMessage = computed(() => problemMessage(this.board.error()));
  protected readonly legend = Object.entries(STATUS_COLORS)
    .filter(([s]) => s !== 'New' && s !== 'Cancelled')
    .map(([status, color]) => ({ status, color, label: statusLabel(status as BoardJob['status']) }));
  private readonly ready = signal(false);

  constructor() {
    afterNextRender(() => {
      this.map = L.map(this.container().nativeElement).setView(DUBAI, 11);
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors',
      }).addTo(this.map);
      this.layer.addTo(this.map);
      this.ready.set(true);
    });
    effect(() => {
      if (this.ready()) this.draw(this.pinned());
    });
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }

  protected shift(days: number): string {
    return addDays(this.date(), days);
  }

  protected pick(date: string): void {
    if (date) this.date.set(date);
  }

  private draw(jobs: BoardJob[]): void {
    this.layer.clearLayers();
    const points: L.LatLngTuple[] = [];
    for (const j of jobs) {
      const at: L.LatLngTuple = [j.latitude!, j.longitude!];
      points.push(at);
      const time = j.scheduledStart && j.scheduledEnd ? `${formatDubai(j.scheduledStart)} to ${formatDubai(j.scheduledEnd).slice(-5)}` : '';
      L.circleMarker(at, { radius: 9, weight: 2, color: '#263238', fillColor: STATUS_COLORS[j.status], fillOpacity: 0.9 })
        .bindPopup(
          `<a href="/office/work-orders/${j.id}"><strong>${escapeHtml(j.number)}</strong></a> · ${escapeHtml(statusLabel(j.status))}<br>` +
            `${escapeHtml(j.title)}<br>${escapeHtml(j.customerName)}, ${escapeHtml(j.siteAddress)}<br>${time}`,
        )
        .addTo(this.layer);
    }
    if (points.length === 1) this.map?.setView(points[0], 14);
    else if (points.length > 1) this.map?.fitBounds(L.latLngBounds(points), { padding: [32, 32] });
  }
}
