import { ChangeDetectionStrategy, Component, OnDestroy, effect, inject, input, signal } from '@angular/core';
import { FieldApi } from '../../features/tech/field.api';

/** Shows an attachment image, fetched with the user's token and shown through an object URL. */
@Component({
  selector: 'app-secure-image',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (url(); as src) {
      <img [src]="src" [alt]="alt()" />
    } @else {
      <div class="placeholder" [attr.aria-label]="alt()"></div>
    }
  `,
  styles: `
    :host { display: block; }
    img, .placeholder { width: 100%; height: 100%; object-fit: cover; border-radius: 6px; display: block; }
    .placeholder { background: var(--mat-sys-surface-container-high); }
  `,
})
export class SecureImage implements OnDestroy {
  private readonly api = inject(FieldApi);
  readonly attachmentId = input.required<string>();
  readonly alt = input('');
  protected readonly url = signal<string | null>(null);

  constructor() {
    effect((onCleanup) => {
      const subscription = this.api.file(this.attachmentId()).subscribe((blob) => this.show(URL.createObjectURL(blob)));
      onCleanup(() => subscription.unsubscribe());
    });
  }

  ngOnDestroy(): void {
    this.show(null);
  }

  private show(url: string | null): void {
    const previous = this.url();
    if (previous) URL.revokeObjectURL(previous);
    this.url.set(url);
  }
}
