import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-my-jobs',
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>My jobs</h1>
    <div class="empty">
      <mat-icon>event_available</mat-icon>
      <p>You have no jobs assigned.</p>
    </div>
  `,
  styles: `
    .empty { display: flex; flex-direction: column; align-items: center; padding: 48px 0; color: var(--mat-sys-on-surface-variant); }
    .empty mat-icon { font-size: 48px; width: 48px; height: 48px; }
  `,
})
export class MyJobs {}
