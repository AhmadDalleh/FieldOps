import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-office-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatSidenavModule, MatToolbarModule, MatListModule, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-sidenav-container class="shell">
      <mat-sidenav mode="side" opened class="nav">
        <div class="brand">FieldOps</div>
        <mat-nav-list>
          <a mat-list-item routerLink="dashboard" routerLinkActive="active">
            <mat-icon matListItemIcon>dashboard</mat-icon><span matListItemTitle>Dashboard</span>
          </a>
          <a mat-list-item routerLink="customers" routerLinkActive="active">
            <mat-icon matListItemIcon>business</mat-icon><span matListItemTitle>Customers</span>
          </a>
          @if (isAdmin()) {
            <a mat-list-item routerLink="settings/users" routerLinkActive="active">
              <mat-icon matListItemIcon>group</mat-icon><span matListItemTitle>Users</span>
            </a>
            <a mat-list-item routerLink="settings/company" routerLinkActive="active">
              <mat-icon matListItemIcon>settings</mat-icon><span matListItemTitle>Company settings</span>
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content>
        <mat-toolbar>
          <span class="spacer"></span>
          <span class="user">{{ auth.user()?.fullName }}</span>
          <a mat-button routerLink="account/password">Change password</a>
          <button mat-button (click)="auth.logout()">Log out</button>
        </mat-toolbar>
        <main class="content"><router-outlet /></main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    .shell { height: 100vh; }
    .nav { width: 232px; }
    .brand { font: var(--mat-sys-title-large); padding: 20px 16px 8px; }
    .active { background: var(--mat-sys-secondary-container); }
    .spacer { flex: 1; }
    .user { font: var(--mat-sys-body-medium); margin-right: 8px; }
    .content { padding: 24px; }
  `,
})
export class OfficeShell {
  protected readonly auth = inject(AuthService);
  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
}
