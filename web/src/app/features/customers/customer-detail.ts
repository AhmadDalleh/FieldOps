import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { problemMessage } from '../../core/problem';
import { CustomerAssets } from '../assets/customer-assets';
import { CustomerWorkOrders } from '../work-orders/customer-work-orders';
import { ConfirmDialog, ConfirmData } from '../../shared/ui/confirm-dialog/confirm-dialog';
import { ContactDialog, ContactDialogData } from './contact-dialog';
import { CustomerDialog } from './customer-dialog';
import { Contact, Customer, CustomersApi, Site } from './customers.api';
import { SiteDialog, SiteDialogData } from './site-dialog';

@Component({
  selector: 'app-customer-detail',
  imports: [MatTabsModule, MatButtonModule, MatTableModule, MatChipsModule, MatSlideToggleModule, RouterLink, CustomerAssets, CustomerWorkOrders],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/office/customers" class="back">← Customers</a>
    @if (customer.value(); as c) {
      <header class="header">
        <div>
          <h1>{{ c.name }}</h1>
          <p class="sub">{{ c.code }} · {{ c.type }} · {{ c.isActive ? 'Active' : 'Inactive' }}</p>
        </div>
        <div class="actions">
          <button mat-stroked-button (click)="edit(c)">Edit</button>
          @if (c.isActive) {
            <button mat-button (click)="deactivate(c)">Deactivate</button>
          }
        </div>
      </header>

      <mat-tab-group>
        <mat-tab label="Info">
          <dl class="info">
            <dt>Phone</dt><dd>{{ c.phone }}</dd>
            <dt>Email</dt><dd>{{ c.email || '—' }}</dd>
            <dt>TRN</dt><dd>{{ c.taxRegistrationNumber || '—' }}</dd>
            <dt>Billing address</dt><dd>{{ c.billingAddress || '—' }}</dd>
            <dt>Notes</dt><dd>{{ c.notes || '—' }}</dd>
          </dl>
        </mat-tab>

        <mat-tab label="Contacts">
          <div class="tab-actions"><button mat-flat-button (click)="openContact(null)">Add contact</button></div>
          <table mat-table [dataSource]="contacts.value() ?? []">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Name</th>
              <td mat-cell *matCellDef="let x">
                {{ x.name }}
                @if (x.isPrimary) {
                  <mat-chip class="primary">Primary</mat-chip>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="jobTitle">
              <th mat-header-cell *matHeaderCellDef>Job title</th>
              <td mat-cell *matCellDef="let x">{{ x.jobTitle }}</td>
            </ng-container>
            <ng-container matColumnDef="phone">
              <th mat-header-cell *matHeaderCellDef>Phone</th>
              <td mat-cell *matCellDef="let x">{{ x.phone }}</td>
            </ng-container>
            <ng-container matColumnDef="email">
              <th mat-header-cell *matHeaderCellDef>Email</th>
              <td mat-cell *matCellDef="let x">{{ x.email }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let x" class="row-actions">
                <button mat-button (click)="openContact(x)">Edit</button>
                <button mat-button (click)="removeContact(x)">Remove</button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="contactColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: contactColumns"></tr>
          </table>
          @if (contacts.value()?.length === 0) {
            <p class="empty">No contacts yet.</p>
          }
        </mat-tab>

        <mat-tab label="Sites">
          <div class="tab-actions">
            <mat-slide-toggle (change)="showInactiveSites.set($event.checked)">Show inactive</mat-slide-toggle>
            <button mat-flat-button (click)="openSite(null)">Add site</button>
          </div>
          <table mat-table [dataSource]="sites.value() ?? []">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Site</th>
              <td mat-cell *matCellDef="let s">{{ s.name }}</td>
            </ng-container>
            <ng-container matColumnDef="address">
              <th mat-header-cell *matHeaderCellDef>Address</th>
              <td mat-cell *matCellDef="let s">{{ s.addressLine1 }}{{ s.addressLine2 ? ', ' + s.addressLine2 : '' }}, {{ s.city }}</td>
            </ng-container>
            <ng-container matColumnDef="pin">
              <th mat-header-cell *matHeaderCellDef>Map pin</th>
              <td mat-cell *matCellDef="let s">{{ s.latitude != null ? 'Yes' : 'No' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let s">{{ s.isActive ? 'Active' : 'Inactive' }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let s" class="row-actions">
                <button mat-button (click)="openSite(s)">Edit</button>
                @if (s.isActive) {
                  <button mat-button (click)="deactivateSite(s)">Deactivate</button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="siteColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: siteColumns"></tr>
          </table>
          @if (sites.value()?.length === 0) {
            <p class="empty">No sites yet.</p>
          }
        </mat-tab>

        <mat-tab label="Assets">
          <ng-template matTabContent>
            <app-customer-assets [customerId]="c.id" [sites]="allSites.value() ?? []" />
          </ng-template>
        </mat-tab>
        <mat-tab label="Work orders">
          <ng-template matTabContent>
            <app-customer-work-orders [customerId]="c.id" [customerName]="c.name" [customerActive]="c.isActive" />
          </ng-template>
        </mat-tab>
        <mat-tab label="Invoices"><p class="empty">Invoices arrive in Phase 8.</p></mat-tab>
      </mat-tab-group>
    }
  `,
  styles: `
    .back { color: var(--mat-sys-primary); text-decoration: none; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; flex-wrap: wrap; }
    .header h1 { margin-bottom: 4px; }
    .sub { margin-top: 0; color: var(--mat-sys-on-surface-variant); }
    .actions { display: flex; gap: 8px; }
    .info { display: grid; grid-template-columns: 160px 1fr; gap: 8px 16px; padding: 16px 0; }
    .info dt { color: var(--mat-sys-on-surface-variant); }
    .info dd { margin: 0; white-space: pre-line; }
    .tab-actions { display: flex; justify-content: flex-end; align-items: center; gap: 16px; padding: 16px 0 8px; }
    .row-actions { text-align: right; white-space: nowrap; }
    .primary { margin-left: 8px; }
    .empty { padding: 16px 0; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class CustomerDetail {
  private readonly api = inject(CustomersApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  /** Bound from the :id route parameter. */
  readonly id = input.required<string>();

  protected readonly contactColumns = ['name', 'jobTitle', 'phone', 'email', 'actions'];
  protected readonly siteColumns = ['name', 'address', 'pin', 'status', 'actions'];
  protected readonly showInactiveSites = signal(false);

  protected readonly customer = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.get(params) });
  protected readonly contacts = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.contacts(params) });
  protected readonly sites = rxResource({
    params: () => ({ id: this.id(), includeInactive: this.showInactiveSites() }),
    stream: ({ params }) => this.api.sites(params.id, params.includeInactive),
  });

  /** Every site, active or not, so assets can be registered against the active ones. */
  protected readonly allSites = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.sites(params, true) });

  protected edit(customer: Customer): void {
    this.dialog
      .open(CustomerDialog, { data: customer })
      .afterClosed()
      .subscribe((saved) => saved && this.customer.reload());
  }

  protected deactivate(customer: Customer): void {
    this.confirm({ title: 'Deactivate customer?', message: `${customer.name} will be hidden from lists.`, confirmLabel: 'Deactivate' })
      .pipe(switchMap(() => this.api.deactivate(customer.id)))
      .subscribe({ next: () => this.customer.reload(), error: (err: unknown) => this.fail(err) });
  }

  protected openContact(contact: Contact | null): void {
    const data: ContactDialogData = { customerId: this.id(), contact };
    this.dialog
      .open(ContactDialog, { data })
      .afterClosed()
      .subscribe((saved) => saved && this.contacts.reload());
  }

  protected removeContact(contact: Contact): void {
    this.confirm({ title: 'Remove contact?', message: `${contact.name} will be removed.`, confirmLabel: 'Remove' })
      .pipe(switchMap(() => this.api.deleteContact(this.id(), contact.id)))
      .subscribe({ next: () => this.contacts.reload(), error: (err: unknown) => this.fail(err) });
  }

  protected openSite(site: Site | null): void {
    const data: SiteDialogData = { customerId: this.id(), site };
    this.dialog
      .open(SiteDialog, { data, width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((saved) => saved && this.reloadSites());
  }

  protected deactivateSite(site: Site): void {
    this.confirm({ title: 'Deactivate site?', message: `${site.name} will be hidden from lists.`, confirmLabel: 'Deactivate' })
      .pipe(switchMap(() => this.api.deactivateSite(site.id)))
      .subscribe({ next: () => this.reloadSites(), error: (err: unknown) => this.fail(err) });
  }

  private reloadSites(): void {
    this.sites.reload();
    this.allSites.reload();
  }

  private confirm(data: ConfirmData) {
    return this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .pipe(filter((ok): ok is true => ok === true));
  }

  private fail(err: unknown): void {
    this.snackBar.open(problemMessage(err), 'Close', { duration: 5000 });
  }
}
