import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { CustomerDialog } from './customer-dialog';

describe('CustomerDialog', () => {
  let backend: HttpTestingController;
  const close = vi.fn();

  beforeEach(() => {
    close.mockReset();
    TestBed.configureTestingModule({
      imports: [CustomerDialog],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: null },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function fillAndSubmit(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(CustomerDialog);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const type = (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    type('input[formcontrolname=name]', 'Acme');
    type('input[formcontrolname=phone]', '+971500000000');
    await fixture.whenStable();
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    return el;
  }

  it('asks to confirm a duplicate phone and then saves with force', async () => {
    const el = await fillAndSubmit();

    const first = backend.expectOne((r) => r.url === '/api/customers');
    expect(first.request.params.has('force')).toBe(false);
    first.flush({ code: 'Customer.DuplicatePhone', title: 'Duplicate' }, { status: 409, statusText: 'Conflict' });
    TestBed.tick();

    expect(el.textContent).toContain('already exists');
    const saveAnyway = [...el.querySelectorAll('button')].find((b) => b.textContent?.includes('Save anyway'))!;
    saveAnyway.click();

    const forced = backend.expectOne((r) => r.url === '/api/customers');
    expect(forced.request.params.get('force')).toBe('true');
    forced.flush({ id: 'c1' });
    expect(close).toHaveBeenCalledWith({ id: 'c1' });
  });

  it('shows other errors without offering to force', async () => {
    const el = await fillAndSubmit();

    backend
      .expectOne('/api/customers')
      .flush({ title: 'One or more validation errors occurred.' }, { status: 400, statusText: 'Bad Request' });
    TestBed.tick();

    expect(el.textContent).toContain('validation errors');
    expect(el.textContent).not.toContain('Save anyway');
  });
});
