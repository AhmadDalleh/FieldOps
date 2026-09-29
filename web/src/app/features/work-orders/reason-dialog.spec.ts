import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { ReasonDialog } from './reason-dialog';

describe('ReasonDialog', () => {
  const close = vi.fn();

  beforeEach(() => {
    close.mockReset();
    TestBed.configureTestingModule({
      imports: [ReasonDialog],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: { title: 'Cancel WO-1', label: 'Reason', confirm: 'Cancel job' } },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
  });

  it('needs text before it confirms and closes with the trimmed reason', async () => {
    const fixture = TestBed.createComponent(ReasonDialog);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const submit = el.querySelector<HTMLButtonElement>('button[type=submit]')!;
    expect(submit.disabled).toBe(true);

    const text = el.querySelector('textarea')!;
    text.value = '  Customer postponed  ';
    text.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    submit.click();

    expect(close).toHaveBeenCalledWith('Customer postponed');
  });
});
