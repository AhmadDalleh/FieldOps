import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { NotificationBell } from '../../features/notifications/notification-bell';

@Component({
  selector: 'app-office-shell',
  imports: [NotificationBell, RouterOutlet, RouterLink, RouterLinkActive, MatSidenavModule, MatToolbarModule, MatListModule, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-sidenav-container class="shell">
      <mat-sidenav mode="side" opened class="nav">
        <div class="brand">FieldOps</div>
        <mat-nav-list>
          <a mat-list-item routerLink="dashboard" routerLinkActive="active">
            <mat-icon matListItemIcon>dashboard</mat-icon><span matListItemTitle>Dashboard</span>
          </a>
          <a mat-list-item routerLink="work-orders" routerLinkActive="active">
            <mat-icon matListItemIcon>assignment</mat-icon><span matListItemTitle>Work orders</span>
          </a>
          <a mat-list-item routerLink="dispatch" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
            <mat-icon matListItemIcon>view_timeline</mat-icon><span matListItemTitle>Dispatch board</span>
          </a>
          <a mat-list-item routerLink="dispatch/map" routerLinkActive="active">
            <mat-icon matListItemIcon>map</mat-icon><span matListItemTitle>Map</span>
          </a>
          <a mat-list-item routerLink="customers" routerLinkActive="active">
            <mat-icon matListItemIcon>business</mat-icon><span matListItemTitle>Customers</span>
          </a>
          <a mat-list-item routerLink="invoices" routerLinkActive="active">
            <mat-icon matListItemIcon>receipt_long</mat-icon><span matListItemTitle>Invoices</span>
          </a>
          <a mat-list-item routerLink="inventory/parts" routerLinkActive="active">
            <mat-icon matListItemIcon>inventory_2</mat-icon><span matListItemTitle>Parts</span>
          </a>
          <a mat-list-item routerLink="inventory/stock" routerLinkActive="active">
            <mat-icon matListItemIcon>warehouse</mat-icon><span matListItemTitle>Stock</span>
          </a>
          <a mat-list-item routerLink="technicians" routerLinkActive="active">
            <mat-icon matListItemIcon>engineering</mat-icon><span matListItemTitle>Technicians</span>
          </a>
          <a mat-list-item routerLink="time-off" routerLinkActive="active">
            <mat-icon matListItemIcon>event_busy</mat-icon><span matListItemTitle>Time off</span>
          </a>
          @if (isAdmin()) {
            <a mat-list-item routerLink="settings/users" routerLinkActive="active">
              <mat-icon matListItemIcon>group</mat-icon><span matListItemTitle>Users</span>
            </a>
            <a mat-list-item routerLink="settings/skills" routerLinkActive="active">
              <mat-icon matListItemIcon>construction</mat-icon><span matListItemTitle>Skills</span>
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
          <app-notification-bell />
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
