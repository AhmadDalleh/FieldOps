import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, afterNextRender, effect, model, viewChild } from '@angular/core';
import * as L from 'leaflet';

export interface LatLng {
  lat: number;
  lng: number;
}

/** Dubai city centre, used when no pin has been dropped yet. */
const DEFAULT_CENTER: LatLng = { lat: 25.2048, lng: 55.2708 };

/** Round to 6 decimals (about 10 cm), which is plenty for a site pin. */
export function roundCoordinate(value: number): number {
  return Math.round(value * 1e6) / 1e6;
}

@Component({
  selector: 'app-map-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div #map class="map" role="application" aria-label="Click the map to drop the site pin"></div>`,
  styles: `.map { height: 280px; border-radius: 8px; border: 1px solid var(--mat-sys-outline-variant); }`,
})
export class MapPicker implements OnDestroy {
  /** The pin location. Clicking the map sets it; setting it moves the pin. */
  readonly value = model<LatLng | null>(null);

  private readonly container = viewChild.required<ElementRef<HTMLElement>>('map');
  private map?: L.Map;
  private marker?: L.CircleMarker;

  constructor() {
    afterNextRender(() => {
      const start = this.value() ?? DEFAULT_CENTER;
      this.map = L.map(this.container().nativeElement).setView([start.lat, start.lng], this.value() ? 15 : 11);
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors',
      }).addTo(this.map);
      this.map.on('click', (e: L.LeafletMouseEvent) =>
        this.value.set({ lat: roundCoordinate(e.latlng.lat), lng: roundCoordinate(e.latlng.lng) }),
      );
      this.showPin(this.value());
    });

    effect(() => this.showPin(this.value()));
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }

  private showPin(value: LatLng | null): void {
    if (!this.map) return;
    if (!value) {
      this.marker?.remove();
      this.marker = undefined;
      return;
    }
    // A circle marker needs no image assets, unlike Leaflet's default pin.
    this.marker ??= L.circleMarker([value.lat, value.lng], { radius: 9, weight: 3 }).addTo(this.map);
    this.marker.setLatLng([value.lat, value.lng]);
  }
}
