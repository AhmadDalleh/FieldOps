import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../core/problem';
import { dubaiLocalToUtc, utcToDubaiLocal } from '../../shared/time/dubai-time';
import { WarrantyBadge } from '../../shared/ui/warranty-badge/warranty-badge';
import { AssetsApi } from '../assets/assets.api';
import { CustomersApi } from '../customers/customers.api';
import { TechniciansApi } from '../technicians/technicians.api';
import { PRIORITIES, TYPES, WorkOrder, WorkOrderPriority, WorkOrderType, WorkOrdersApi } from './work-orders.api';

export interface WorkOrderDialogData {
  /** The work order to edit; null to create one. */
  workOrder: WorkOrder | null;
  /** Preselects the customer when creating from a customer's page. */
  customer?: { id: string; name: string };
}

@Component({
  selector: 'app-work-order-dialog',
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatAutocompleteModule,
    MatButtonModule, WarrantyBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ editing ? 'Edit ' + editing.number : 'New work order' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        @if (editing) {
          <p class="context">{{ editing.customer.name }} · {{ editing.site.name }}</p>
        } @else {
          <mat-form-field appearance="outline">
            <mat-label>Customer</mat-label>
            <input matInput placeholder="Search by name, code or phone" [value]="customerName()" (input)="searchCustomers($any($event.target).value)" [matAutocomplete]="customerAuto" />
            <mat-autocomplete #customerAuto (optionSelected)="pickCustomer($event.option.value)">
              @for (c of customers.value()?.items ?? []; track c.id) {
                <mat-option [value]="c">{{ c.name }} <span class="muted">· {{ c.code }} · {{ c.phone }}</span></mat-option>
              }
            </mat-autocomplete>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Site</mat-label>
            <mat-select formControlName="siteId">
              @for (s of sites.value() ?? []; track s.id) {
                <mat-option [value]="s.id">{{ s.name }} · {{ s.addressLine1 }}, {{ s.city }}</mat-option>
              }
            </mat-select>
            @if (customerId() && sites.hasValue() && sites.value().length === 0) {
              <mat-hint>This customer has no active sites yet.</mat-hint>
            }
          </mat-form-field>
        }
        <mat-form-field appearance="outline">
          <mat-label>Asset (optional)</mat-label>
          <mat-select formControlName="assetId">
            <mat-option [value]="''">None</mat-option>
            @for (a of siteAssets(); track a.id) {
              <mat-option [value]="a.id">{{ a.name }}{{ a.serialNumber ? ' · ' + a.serialNumber : '' }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (selectedAsset(); as a) {
          <app-warranty-badge class="badge" [expiresOn]="a.warrantyExpiresOn" [underWarranty]="a.underWarranty" />
        }
        <mat-form-field appearance="outline"><mat-label>Title</mat-label><input matInput formControlName="title" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Description</mat-label><textarea matInput formControlName="description" rows="3"></textarea></mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Type</mat-label>
            <mat-select formControlName="type">
              @for (t of types; track t) {
                <mat-option [value]="t">{{ t }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority">
              @for (p of priorities; track p) {
                <mat-option [value]="p">{{ p }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Required skill (optional)</mat-label>
          <mat-select formControlName="requiredSkillId">
            <mat-option [value]="''">None</mat-option>
            @for (s of skills.value() ?? []; track s.id) {
              <mat-option [value]="s.id">{{ s.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Due by (Dubai time)</mat-label>
          <input matInput type="datetime-local" formControlName="dueBy" />
          @if (priority() === 'Urgent' && !dueBy() && !editing) {
            <mat-hint>Urgent jobs without a due time are due in 4 hours.</mat-hint>
          }
        </mat-form-field>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || busy()">{{ editing ? 'Save' : 'Create' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields { display: flex; flex-direction: column; min-width: min(560px, 80vw); }
    .row { display: flex; gap: 16px; flex-wrap: wrap; }
    .row mat-form-field { flex: 1; min-width: 180px; }
    .context { margin: 0 0 12px; color: var(--mat-sys-on-surface-variant); }
    .badge { margin: -8px 0 16px; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .error { color: var(--mat-sys-error); }
  `,
})
export class WorkOrderDialog {
  private readonly api = inject(WorkOrdersApi);
  private readonly customersApi = inject(CustomersApi);
  private readonly assetsApi = inject(AssetsApi);
  private readonly ref = inject(MatDialogRef<WorkOrderDialog, WorkOrder>);
  private readonly data = inject<WorkOrderDialogData>(MAT_DIALOG_DATA);
  protected readonly editing = this.data.workOrder;
  protected readonly types = TYPES;
  protected readonly priorities = PRIORITIES;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    customerId: [this.editing?.customer.id ?? this.data.customer?.id ?? '', Validators.required],
    siteId: [this.editing?.site.id ?? '', Validators.required],
    assetId: [this.editing?.asset?.id ?? ''],
    title: [this.editing?.title ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.editing?.description ?? '', Validators.maxLength(4000)],
    type: [this.editing?.type ?? ('Repair' as WorkOrderType), Validators.required],
    priority: [this.editing?.priority ?? ('Medium' as WorkOrderPriority), Validators.required],
    dueBy: [this.editing?.dueBy ? utcToDubaiLocal(this.editing.dueBy) : ''],
    requiredSkillId: [this.editing?.requiredSkill?.id ?? ''],
  });

  protected readonly customerName = signal(this.editing?.customer.name ?? this.data.customer?.name ?? '');
  private readonly search = signal('');
  protected readonly customerId = toSignal(this.form.controls.customerId.valueChanges, { initialValue: this.form.controls.customerId.value });
  private readonly siteId = toSignal(this.form.controls.siteId.valueChanges, { initialValue: this.form.controls.siteId.value });
  protected readonly priority = toSignal(this.form.controls.priority.valueChanges, { initialValue: this.form.controls.priority.value });
  protected readonly dueBy = toSignal(this.form.controls.dueBy.valueChanges, { initialValue: this.form.controls.dueBy.value });
  private readonly assetId = toSignal(this.form.controls.assetId.valueChanges, { initialValue: this.form.controls.assetId.value });

  protected readonly customers = rxResource({
    params: () => (this.editing ? undefined : this.search()),
    stream: ({ params }) =>
      this.customersApi.list({ page: 1, pageSize: 10, search: params, includeInactive: false }),
  });
  protected readonly sites = rxResource({
    params: () => (this.editing ? undefined : this.customerId() || undefined),
    stream: ({ params }) => this.customersApi.sites(params, false),
  });
  private readonly techniciansApi = inject(TechniciansApi);
  protected readonly skills = rxResource({ stream: () => this.techniciansApi.skills() });
  private readonly assets = rxResource({
    params: () => this.customerId() || undefined,
    stream: ({ params }) => this.assetsApi.forCustomer(params),
  });
  protected readonly siteAssets = computed(() =>
    (this.assets.value() ?? []).filter((a) => a.siteId === this.siteId() && (a.isActive || a.id === this.editing?.asset?.id)),
  );
  protected readonly selectedAsset = computed(() => this.siteAssets().find((a) => a.id === this.assetId()) ?? null);

  constructor() {
    if (this.data.customer && !this.editing) this.pickCustomer(this.data.customer);
  }

  protected searchCustomers(term: string): void {
    this.customerName.set(term);
    this.search.set(term);
    if (this.form.controls.customerId.value) {
      this.form.patchValue({ customerId: '', siteId: '', assetId: '' });
    }
  }

  protected pickCustomer(customer: { id: string; name: string }): void {
    this.customerName.set(customer.name);
    this.form.patchValue({ customerId: customer.id, siteId: '', assetId: '' });
    // Preselect the only site so the common case is one click shorter.
    this.customersApi.sites(customer.id, false).subscribe((sites) => {
      if (sites.length === 1 && !this.form.controls.siteId.value) this.form.controls.siteId.setValue(sites[0].id);
    });
  }

  protected save(): void {
    const v = this.form.getRawValue();
    const common = {
      title: v.title.trim(),
      description: v.description.trim() || null,
      type: v.type,
      priority: v.priority,
      dueBy: v.dueBy ? dubaiLocalToUtc(v.dueBy) : null,
      assetId: v.assetId || null,
      requiredSkillId: v.requiredSkillId || null,
    };
    const request = this.editing
      ? this.api.update(this.editing.id, { ...common, version: this.editing.version })
      : this.api.create({ ...common, customerId: v.customerId, siteId: v.siteId });
    this.busy.set(true);
    request.subscribe({
      next: (wo) => this.ref.close(wo),
      error: (err: unknown) => {
        this.error.set(problemMessage(err));
        this.busy.set(false);
      },
    });
  }
}
