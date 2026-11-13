import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import {
  RespondItemDialogComponent,
  RespondItemDialogData,
  RespondItemDialogResult,
} from './respond-item-dialog.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { ResponseInput } from '../../../core/models';

const BASE = '/api/v1';
const INPUT_URL = `${BASE}/audits/a-1/items/i-1/response-input`;

describe('RespondItemDialogComponent', () => {
  let fixture: ComponentFixture<RespondItemDialogComponent>;
  let component: RespondItemDialogComponent;
  let http: HttpTestingController;
  let closed: RespondItemDialogResult | undefined;

  function setup(data: Partial<RespondItemDialogData>): void {
    closed = undefined;
    TestBed.configureTestingModule({
      imports: [RespondItemDialogComponent],
      providers: [
        provideTestEnv(),
        {
          provide: MAT_DIALOG_DATA,
          useValue: { auditId: 'a-1', itemId: 'i-1', prompt: 'Is it controlled?', responseType: 'pass_fail_na', ...data },
        },
        { provide: MatDialogRef, useValue: { close: (r?: RespondItemDialogResult) => (closed = r) } },
      ],
    });
    fixture = TestBed.createComponent(RespondItemDialogComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  function flushInput(input: Partial<ResponseInput>): void {
    http.expectOne(INPUT_URL).flush({ data: { responseType: 'pass_fail_na', ...input } });
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => http.verify());

  it("asks the audit — not the template library — what the item offers, and shows the organisation's labels", () => {
    setup({ responseType: 'pass_fail_na' });
    flushInput({
      options: [
        { code: 'ok', label: 'Compliant', isDeficiency: false, isNotApplicable: false, requiresComment: false },
        { code: 'bad', label: 'Non-compliant', isDeficiency: true, isNotApplicable: false, requiresComment: true },
      ],
    });

    expect(text()).toContain('Compliant');
    expect(text()).toContain('Non-compliant');
    expect(component.canSubmit()).toBe(false);

    component.form.controls.selectedOptionCode.setValue('ok');
    expect(component.canSubmit()).toBe(true);

    component.submit();
    expect(closed?.selectedOptionCode).toBe('ok');
    expect(closed?.verdict).toBeNull(); // the server derives it from the option
  });

  it('a finding option needs a comment before it can be submitted', () => {
    setup({ responseType: 'yes_no' });
    flushInput({
      responseType: 'yes_no',
      options: [
        { code: 'pass', label: 'Yes', isDeficiency: false, isNotApplicable: false, requiresComment: false },
        { code: 'fail', label: 'No', isDeficiency: true, isNotApplicable: false, requiresComment: true },
      ],
    });

    component.form.controls.selectedOptionCode.setValue('fail');
    expect(component.canSubmit()).toBe(false);

    component.form.controls.comment.setValue('Control not in place.');
    expect(component.canSubmit()).toBe(true);
  });

  it('falls back to the built-in conclusions if the item definition cannot be loaded', () => {
    setup({ responseType: 'pass_fail_na' });
    http.expectOne(INPUT_URL).flush({ detail: 'nope' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.usesOptionSet()).toBe(false);
    component.form.controls.verdict.setValue('pass');
    component.submit();
    expect(closed?.verdict).toBe('pass');
    expect(closed?.selectedOptionCode).toBeNull();
  });

  it('pre-selects the matching option when re-opening a response saved with a bare verdict', () => {
    setup({
      responseType: 'pass_fail_na',
      current: {
        id: 'r-1', auditId: 'a-1', checklistItemId: 'i-1', verdict: 'fail', comment: 'Missing sign-off.',
        responderUserId: 'u-1', isDraft: false, responseVersion: 1,
      },
    });
    flushInput({
      options: [
        { code: 'pass', label: 'Pass', isDeficiency: false, isNotApplicable: false, requiresComment: false },
        { code: 'fail', label: 'Fail', isDeficiency: true, isNotApplicable: false, requiresComment: true },
        { code: 'na', label: 'N/A', isDeficiency: false, isNotApplicable: true, requiresComment: true },
      ],
    });

    expect(component.form.controls.selectedOptionCode.value).toBe('fail');
  });

  it("a rating offers the scale's labelled points", () => {
    setup({ responseType: 'rating', responseConfigJson: '{"ratingScaleId":"s-1"}' });
    flushInput({ responseType: 'rating', ratingPoints: [{ value: 1, label: 'Weak' }, { value: 2, label: 'Strong' }] });

    expect(text()).toContain('1 – Weak');
    expect(text()).toContain('2 – Strong');

    component.form.controls.value.setValue('2');
    component.submit();
    expect(closed?.valueJson).toBe('{"rating":2}');
  });

  it('a multiple-choice item only accepts one of its choices', () => {
    setup({ responseType: 'multiple_choice' });
    flushInput({ responseType: 'multiple_choice', choices: ['Fully', 'Partially'] });

    expect(text()).toContain('Fully');
    component.form.controls.value.setValue('Something else');
    expect(component.canSubmit()).toBe(false);

    component.form.controls.value.setValue('Partially');
    expect(component.canSubmit()).toBe(true);
    component.submit();
    expect(closed?.valueJson).toBe('{"choice":"Partially"}');
  });

  it('says so — instead of showing an empty form — when a multiple-choice item has no choices', () => {
    setup({ responseType: 'multiple_choice' });
    flushInput({ responseType: 'multiple_choice', choices: [] });

    expect(text()).toContain('no choices to pick from');
    expect(component.canSubmit()).toBe(false);
  });

  it('a number shows its unit and must be a real number', () => {
    setup({ responseType: 'numeric' });
    flushInput({ responseType: 'numeric', unit: 'days' });

    expect(text()).toContain('Value (days)');

    component.form.controls.value.setValue('abc');
    expect(component.canSubmit()).toBe(false);

    component.form.controls.value.setValue('12.5');
    expect(component.canSubmit()).toBe(true);
    component.submit();
    expect(closed?.valueJson).toBe('{"number":12.5}');
  });

  it('date and free-text answers are captured in the shape the API expects', () => {
    setup({ responseType: 'date' });
    flushInput({ responseType: 'date' });
    component.form.controls.value.setValue('2026-11-13');
    component.submit();
    expect(closed?.valueJson).toBe('{"date":"2026-11-13"}');
  });

  it('a draft can be saved without an answer', () => {
    setup({ responseType: 'text' });
    flushInput({ responseType: 'text' });

    expect(component.canSubmit()).toBe(false);
    component.saveDraft();
    expect(closed?.isDraft).toBe(true);
    expect(closed?.valueJson).toBeNull();
  });
});
