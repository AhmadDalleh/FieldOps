import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { WorkOrderPart } from './inventory.api';
import { WorkOrderPartsPanel, partsTotal } from './work-order-parts';

const line = (id: string, lineTotal: number, canRemove = true): WorkOrderPart => ({
  id, partId: 'p-' + id, sku: 'SKU-' + id, name: 'Part ' + id, unit: 'Pcs', quantity: 1, unitPrice: lineTotal, lineTotal,
  locationName: 'Van · Sara', canRemove,
});

describe('partsTotal', async () => {
  it('adds line totals without floating point noise', async () => {
    expect(partsTotal([line('a', 0.1), line('b', 0.2)])).toBe(0.3);
    expect(partsTotal([])).toBe(0);
  });
});

describe('WorkOrderPartsPanel', async () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [WorkOrderPartsPanel], providers: [provideHttpClient(), provideHttpClientTesting()] });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('lists the lines read-only for the office and does not load van stock', async () => {
    const fixture = TestBed.createComponent(WorkOrderPartsPanel);
    fixture.componentRef.setInput('workOrderId', 'wo-1');
    TestBed.tick();
    fixture.detectChanges();
    backend.expectOne('/api/work-orders/wo-1/parts').flush([line('a', 12.5, false)]);
    backend.expectNone('/api/me/van-stock');
    await fixture.whenStable();
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Part a');
    expect(text).toContain('Parts total: 12.50 AED');
    expect(fixture.nativeElement.querySelector('button[aria-label^="Remove"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });

  it('returns a removed line and shows the updated list', async () => {
    const fixture = TestBed.createComponent(WorkOrderPartsPanel);
    fixture.componentRef.setInput('workOrderId', 'wo-1');
    fixture.componentRef.setInput('canAdd', true);
    TestBed.tick();
    fixture.detectChanges();
    backend.expectOne('/api/work-orders/wo-1/parts').flush([line('a', 10), line('b', 5)]);
    backend.expectOne('/api/me/van-stock').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[aria-label="Remove Part a"]') as HTMLButtonElement).click();
    const req = backend.expectOne('/api/work-orders/wo-1/parts/a');
    expect(req.request.method).toBe('DELETE');
    req.flush([line('b', 5)]);
    TestBed.tick();
    backend.expectOne('/api/me/van-stock').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Part a');
    expect(fixture.nativeElement.textContent).toContain('Parts total: 5.00 AED');
  });
});
