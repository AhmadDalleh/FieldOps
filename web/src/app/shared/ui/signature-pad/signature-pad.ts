import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

/** A canvas the customer signs on with a finger, pen or mouse (US-TAPP-08 AC2). */
@Component({
  selector: 'app-signature-pad',
  imports: [MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <canvas
      #canvas
      role="img"
      aria-label="Signature area. Sign here."
      (pointerdown)="down($event)"
      (pointermove)="move($event)"
      (pointerup)="up()"
      (pointercancel)="up()"
      (pointerleave)="up()"
    ></canvas>
    <div class="row">
      <span class="hint">{{ empty() ? 'Sign above' : 'Signed' }}</span>
      <button mat-button type="button" (click)="clear()" [disabled]="empty()">Clear</button>
    </div>
  `,
  styles: `
    :host { display: block; }
    canvas { width: 100%; height: 180px; display: block; touch-action: none; background: #fff;
      border: 1px dashed var(--mat-sys-outline); border-radius: 8px; cursor: crosshair; }
    .row { display: flex; justify-content: space-between; align-items: center; }
    .hint { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class SignaturePad {
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  readonly empty = signal(true);
  private drawing = false;
  private context?: CanvasRenderingContext2D;

  constructor() {
    afterNextRender(() => this.resize());
  }

  /** The signature as a PNG, or null when nothing was drawn. */
  toBlob(): Promise<Blob | null> {
    if (this.empty()) return Promise.resolve(null);
    return new Promise((resolve) => this.canvas().nativeElement.toBlob(resolve, 'image/png'));
  }

  clear(): void {
    const el = this.canvas().nativeElement;
    this.context?.clearRect(0, 0, el.width, el.height);
    this.empty.set(true);
  }

  protected down(event: PointerEvent): void {
    if (!this.context) this.resize();
    this.drawing = true;
    (event.target as HTMLElement).setPointerCapture?.(event.pointerId);
    const { x, y } = this.point(event);
    this.context!.beginPath();
    this.context!.moveTo(x, y);
    this.context!.lineTo(x + 0.1, y + 0.1);
    this.context!.stroke();
    this.empty.set(false);
  }

  protected move(event: PointerEvent): void {
    if (!this.drawing || !this.context) return;
    const { x, y } = this.point(event);
    this.context.lineTo(x, y);
    this.context.stroke();
  }

  protected up(): void {
    this.drawing = false;
  }

  private point(event: PointerEvent): { x: number; y: number } {
    const rect = this.canvas().nativeElement.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  }

  /** Matches the drawing buffer to the displayed size so strokes land under the finger on high-density screens. */
  private resize(): void {
    const el = this.canvas().nativeElement;
    const ratio = window.devicePixelRatio || 1;
    const rect = el.getBoundingClientRect();
    el.width = Math.max(1, Math.round(rect.width * ratio));
    el.height = Math.max(1, Math.round(rect.height * ratio));
    const context = el.getContext('2d');
    if (!context) return;
    context.scale(ratio, ratio);
    context.lineWidth = 2.5;
    context.lineCap = 'round';
    context.lineJoin = 'round';
    context.strokeStyle = '#111';
    this.context = context;
    this.empty.set(true);
  }
}
