import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { vi } from 'vitest';
import { SignaturePad } from '../../../shared/ui/signature-pad/signature-pad';
import { WorkOrder } from '../../work-orders/work-orders.api';
import { CompleteDialog } from './complete-dialog';

const job = {
  id: 'wo-1',
  number: 'WO-000001',
  tasks: [
    { id: 't1', isDone: true },
    { id: 't2', isDone: false },
  ],
} as unknown as WorkOrder;

describe('CompleteDialog', () => {
  let backend: HttpTestingController;
  const close = vi.fn();

  beforeEach(() => {
    close.mockReset();
    TestBed.configureTestingModule({
      imports: [CompleteDialog],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: job },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function signedForm() {
    const fixture = TestBed.createComponent(CompleteDialog);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const type = (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    type('textarea[formcontrolname=completionNotes]', 'Replaced the capacitor');
    type('input[formcontrolname=signedByName]', 'Sara M.');
    const pad = fixture.debugElement.query(By.directive(SignaturePad)).componentInstance as SignaturePad;
    pad.empty.set(false);
    vi.spyOn(pad, 'toBlob').mockResolvedValue(new Blob(['png'], { type: 'image/png' }));
    await fixture.whenStable();
    return { fixture, el, type };
  }

  const submit = (el: HTMLElement) => el.querySelector<HTMLButtonElement>('button[type=submit]')!;

  it('asks why tasks were skipped before it can be submitted', async () => {
    const { fixture, el, type } = await signedForm();

    expect(el.textContent).toContain('Why 1 task(s) were skipped');
    expect(submit(el).disabled).toBe(true);
    type('textarea[formcontrolname=skippedTasksReason]', 'No access to the roof');
    await fixture.whenStable();
    expect(submit(el).disabled).toBe(false);
  });

  it('uploads the signature once, then completes, reusing it after a failed attempt', async () => {
    const { fixture, el, type } = await signedForm();
    type('textarea[formcontrolname=skippedTasksReason]', 'No access to the roof');
    await fixture.whenStable();

    submit(el).click();
    await vi.waitFor(() => backend.expectOne('/api/work-orders/wo-1/attachments').flush({ id: 'sig-1' }));
    const first = await vi.waitFor(() => backend.expectOne('/api/work-orders/wo-1/complete'));
    expect(first.request.body).toEqual({
      completionNotes: 'Replaced the capacitor',
      signedByName: 'Sara M.',
      signatureAttachmentId: 'sig-1',
      skippedTasksReason: 'No access to the roof',
    });
    first.flush({ title: 'A work order that is OnHold cannot Complete.' }, { status: 409, statusText: 'Conflict' });
    await vi.waitFor(() => expect(el.querySelector('[role=alert]')?.textContent).toContain('cannot Complete'));
    await fixture.whenStable();

    submit(el).click();
    const retry = await vi.waitFor(() => backend.expectOne('/api/work-orders/wo-1/complete'));
    backend.expectNone('/api/work-orders/wo-1/attachments');
    retry.flush({ id: 'wo-1', status: 'Completed' });
    await vi.waitFor(() => expect(close).toHaveBeenCalledWith({ id: 'wo-1', status: 'Completed' }));
  });
});
