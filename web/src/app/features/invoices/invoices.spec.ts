import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { fileNameFrom } from '../../shared/files/download';
import { Invoice, formatDay, invoiceName, lineAmount, roundMoney } from './invoices.api';
import { LineDialog, lineProblem } from './line-dialog';

describe('invoice helpers', () => {
  it('rounds money half away from zero like the server', () => {
    expect(roundMoney(9.375)).toBe(9.38);
    expect(roundMoney(-0.125)).toBe(-0.13);
    expect(roundMoney(1.005)).toBe(1.01);
    expect(lineAmount(1.25, 33.33)).toBe(41.66);
    expect(lineAmount(null, 5)).toBe(0);
  });

  it('shows drafts without a number', () => {
    expect(invoiceName({ number: null, status: 'Draft' })).toBe('Draft invoice');
    expect(invoiceName({ number: 'INV-000045', status: 'Issued' })).toBe('INV-000045');
  });

  it('formats API dates without shifting the day', () => {
    expect(formatDay('2026-10-01')).toBe('1 Oct 2026');
    expect(formatDay(null)).toBe('');
  });

  it('reads the download name from Content-Disposition', () => {
    expect(fileNameFrom("attachment; filename=INV-000001.pdf; filename*=UTF-8''INV-000001.pdf", 'x.pdf')).toBe('INV-000001.pdf');
    expect(fileNameFrom('attachment; filename="Draft-WO-000010.pdf"', 'x.pdf')).toBe('Draft-WO-000010.pdf');
    expect(fileNameFrom(null, 'invoice.pdf')).toBe('invoice.pdf');
  });

  it('allows a negative price only on Other lines', () => {
    const line = { lineType: 'Other', description: 'Discount', quantity: 1, unitPrice: -20 };
    expect(lineProblem(line)).toBeNull();
    expect(lineProblem({ ...line, lineType: 'Part' })).toBe('Only Other lines can be negative (for a discount).');
    expect(lineProblem({ ...line, description: ' ' })).toBe('Enter a description.');
    expect(lineProblem({ ...line, quantity: 0 })).toBe('The quantity must be above zero.');
    expect(lineProblem({ ...line, unitPrice: 1.005 })).toBe('Use at most two decimals.');
  });
});

describe('LineDialog', () => {
  const invoice = { id: 'inv-1', currency: 'AED', lines: [] } as unknown as Invoice;
  const close = vi.fn();

  it('adds a call-out fee and closes with the updated invoice', async () => {
    TestBed.configureTestingModule({
      imports: [LineDialog],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { invoice, line: null } },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    const backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(LineDialog);
    fixture.detectChanges();

    const inputs: HTMLInputElement[] = Array.from(fixture.nativeElement.querySelectorAll('input'));
    const [description, , price] = inputs;
    description.value = 'Call-out fee';
    description.dispatchEvent(new Event('input'));
    price.value = '150';
    price.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Line amount: 150.00 AED');
    (fixture.nativeElement.querySelector('button[type=submit]') as HTMLButtonElement).click();

    const req = backend.expectOne('/api/invoices/inv-1/lines');
    expect(req.request.body).toEqual({ lineType: 'Other', description: 'Call-out fee', quantity: 1, unitPrice: 150 });
    req.flush({ ...invoice, total: 157.5 });
    expect(close).toHaveBeenCalledWith(expect.objectContaining({ total: 157.5 }));
    backend.verify();
  });
});
