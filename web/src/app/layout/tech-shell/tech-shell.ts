import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-tech-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="shell">
      <mat-toolbar>
        <span>FieldOps</span>
        <span class="spacer"></span>
        <button mat-icon-button aria-label="Log out" (click)="auth.logout()"><mat-icon>logout</mat-icon></button>
      </mat-toolbar>
      <main class="content"><router-outlet /></main>
      <nav class="bottom-nav">
        <a routerLink="my-jobs" routerLinkActive="active"><mat-icon>work</mat-icon><span>My jobs</span></a>
        <a routerLink="time-off" routerLinkActive="active"><mat-icon>event_busy</mat-icon><span>Time off</span></a>
        <a routerLink="account/password" routerLinkActive="active"><mat-icon>person</mat-icon><span>Account</span></a>
      </nav>
    </div>
  `,
  styles: `
    .shell { display: flex; flex-direction: column; min-height: 100vh; }
    .spacer { flex: 1; }
    .content { flex: 1; padding: 16px; }
    .bottom-nav { position: sticky; bottom: 0; display: flex; border-top: 1px solid var(--mat-sys-outline-variant); background: var(--mat-sys-surface-container); }
    .bottom-nav a { flex: 1; display: flex; flex-direction: column; align-items: center; padding: 8px 0; min-height: 48px; color: var(--mat-sys-on-surface-variant); text-decoration: none; font: var(--mat-sys-label-medium); }
    .bottom-nav a.active { color: var(--mat-sys-primary); }
  `,
})
export class TechShell {
  protected readonly auth = inject(AuthService);
}
