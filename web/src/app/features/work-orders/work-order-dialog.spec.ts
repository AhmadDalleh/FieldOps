import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { WorkOrderDialog } from './work-order-dialog';
import { WorkOrder } from './work-orders.api';

const existing: WorkOrder = {
  id: 'wo-1',
  number: 'WO-000007',
  status: 'Scheduled',
  title: 'AC not cooling',
  description: null,
  type: 'Repair',
  priority: 'High',
  dueBy: '2026-10-01T04:00:00Z',
  isOverdue: false,
  scheduledStart: null,
  scheduledEnd: null,
  customer: { id: 'c-1', code: 'C-00001', name: 'Acme', phone: '+971500000000' },
  site: { id: 's-1', name: 'HQ', addressLine1: 'Road', addressLine2: null, city: 'Dubai', latitude: null, longitude: null, accessNotes: null },
  asset: null,
  technician: null,
  requiredSkill: null,
  startedAt: null,
  completedAt: null,
  completionNotes: null,
  signedByName: null,
  cancelReason: null,
  tasks: [],
  allowedActions: [],
  isEditable: true,
  createdAt: '2026-09-30T04:00:00Z',
  version: 812,
};

describe('WorkOrderDialog', () => {
  let backend: HttpTestingController;
  const close = vi.fn();

  beforeEach(() => {
    close.mockReset();
    TestBed.configureTestingModule({
      imports: [WorkOrderDialog],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { workOrder: existing } },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('edits with the loaded version and sends the due time as UTC', async () => {
    const fixture = TestBed.createComponent(WorkOrderDialog);
    fixture.detectChanges();
    TestBed.tick();
    backend.expectOne('/api/customers/c-1/assets').flush([]);
    backend.expectOne('/api/skills').flush([{ id: 'sk-1', name: 'HVAC' }]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    const due = el.querySelector<HTMLInputElement>('input[formcontrolname=dueBy]')!;
    expect(due.value).toBe('2026-10-01T08:00');
    const title = el.querySelector<HTMLInputElement>('input[formcontrolname=title]')!;
    title.value = 'Replace compressor';
    title.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();

    const req = backend.expectOne('/api/work-orders/wo-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      title: 'Replace compressor',
      description: null,
      type: 'Repair',
      priority: 'High',
      dueBy: '2026-10-01T04:00:00.000Z',
      assetId: null,
      requiredSkillId: null,
      version: 812,
    });
    req.flush({ ...existing, title: 'Replace compressor' });
    expect(close).toHaveBeenCalledWith(expect.objectContaining({ title: 'Replace compressor' }));
  });

  it('shows the reload message when someone else saved first', async () => {
    const fixture = TestBed.createComponent(WorkOrderDialog);
    fixture.detectChanges();
    TestBed.tick();
    backend.expectOne('/api/customers/c-1/assets').flush([]);
    backend.expectOne('/api/skills').flush([{ id: 'sk-1', name: 'HVAC' }]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();

    backend.expectOne('/api/work-orders/wo-1').flush(
      { code: 'WorkOrder.ConcurrencyConflict', title: 'Someone else changed this work order. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    await fixture.whenStable();

    expect(el.querySelector('[role=alert]')!.textContent).toContain('Reload it and try again');
    expect(close).not.toHaveBeenCalled();
  });
});
