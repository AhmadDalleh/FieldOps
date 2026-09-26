import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Welcome, {{ auth.user()?.fullName }}</h1>
    <p>The dashboard arrives in a later phase.</p>
  `,
})
export class Dashboard {
  protected readonly auth = inject(AuthService);
}
