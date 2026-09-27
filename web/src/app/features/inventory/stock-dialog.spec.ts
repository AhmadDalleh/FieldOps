import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { StockLocation, StockRow, isValidQuantity } from './inventory.api';
import { StockDialog, StockForm, quantityAt, stockFormProblem } from './stock-dialog';

const row: StockRow = {
  partId: 'p1', sku: 'FLT-01', name: 'Filter', unit: 'Pcs', reorderLevel: 5, total: 8, isLow: false,
  levels: [{ locationId: 'wh', quantity: 6 }, { locationId: 'van', quantity: 2 }],
};
const locations: StockLocation[] = [
  { id: 'wh', name: 'Main warehouse', type: 'Warehouse', technicianId: null, isActive: true },
  { id: 'van', name: 'Van · Sara', type: 'Van', technicianId: 't1', isActive: true },
];
const form = (v: Partial<StockForm>): StockForm => ({ fromLocationId: '', toLocationId: '', quantity: null, reason: '', ...v });

describe('stock form rules', () => {
  it('accepts quantities above zero with up to two decimals', () => {
    expect(isValidQuantity(1.25)).toBe(true);
    expect(isValidQuantity(0.1 + 0.2)).toBe(true); // 0.30000000000000004 is 0.3 typed into a number input
    expect(isValidQuantity(0.3)).toBe(true);
    expect(isValidQuantity(0)).toBe(false);
    expect(isValidQuantity(1.005)).toBe(false);
  });

  it('reads the quantity at a location, zero when none', () => {
    expect(quantityAt(row, 'van')).toBe(2);
    expect(quantityAt(row, 'nowhere')).toBe(0);
  });

  it('blocks a transfer larger than the source holds', () => {
    expect(stockFormProblem('transfer', row, form({ fromLocationId: 'van', toLocationId: 'wh', quantity: 3 }))).toBe('Only 2 pcs there.');
    expect(stockFormProblem('transfer', row, form({ fromLocationId: 'van', toLocationId: 'wh', quantity: 2 }))).toBeNull();
  });

  it('blocks a transfer to the same place', () => {
    expect(stockFormProblem('transfer', row, form({ fromLocationId: 'wh', toLocationId: 'wh', quantity: 1 }))).toBe('Choose two different locations.');
  });

  it('lets an adjustment count down to zero but needs a reason', () => {
    expect(stockFormProblem('adjust', row, form({ toLocationId: 'van', quantity: 0 }))).toBe('Say why the stock is being adjusted.');
    expect(stockFormProblem('adjust', row, form({ toLocationId: 'van', quantity: 0, reason: 'Stock count' }))).toBeNull();
    expect(stockFormProblem('adjust', row, form({ toLocationId: 'van', quantity: -1, reason: 'x' }))).not.toBeNull();
  });
});

describe('StockDialog', () => {
  const close = vi.fn();

  function create(action: 'receive' | 'transfer') {
    close.mockReset();
    TestBed.configureTestingModule({
      imports: [StockDialog],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { action, row, locations } },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    const fixture = TestBed.createComponent(StockDialog);
    fixture.detectChanges();
    return { fixture, backend: TestBed.inject(HttpTestingController) };
  }

  it('receives into the warehouse and closes with the new row', async () => {
    const { fixture, backend } = create('receive');
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=number]');
    input.value = '4';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    const submit: HTMLButtonElement = fixture.nativeElement.querySelector('button[type=submit]');
    expect(submit.disabled).toBe(false);
    submit.click();

    const req = backend.expectOne('/api/stock/receive');
    expect(req.request.body).toEqual({ partId: 'p1', locationId: 'wh', quantity: 4 });
    req.flush({ ...row, total: 12 });
    expect(close).toHaveBeenCalledWith(expect.objectContaining({ total: 12 }));
    backend.verify();
  });

  it('starts a transfer from the warehouse with the send button disabled', async () => {
    const { fixture } = create('transfer');
    await fixture.whenStable();
    const submit: HTMLButtonElement = fixture.nativeElement.querySelector('button[type=submit]');
    expect(submit.disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Transfer FLT-01');
  });
});
