import { TestBed } from '@angular/core/testing';
import { WarrantyBadge, formatDate } from './warranty-badge';

describe('WarrantyBadge', () => {
  it('formats the date without shifting the day', () => {
    expect(formatDate('2026-10-01')).toBe('1 Oct 2026');
  });

  it('shows the expiry date when the asset is under warranty', async () => {
    const fixture = TestBed.createComponent(WarrantyBadge);
    fixture.componentRef.setInput('expiresOn', '2027-06-01');
    fixture.componentRef.setInput('underWarranty', true);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent.trim()).toBe('Under warranty until 1 Jun 2027');
  });

  it('shows nothing once the warranty has expired', async () => {
    const fixture = TestBed.createComponent(WarrantyBadge);
    fixture.componentRef.setInput('expiresOn', '2020-01-01');
    fixture.componentRef.setInput('underWarranty', false);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });
});
