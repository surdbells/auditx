import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';

import {
  RESPONSE_TYPES,
  buildResponseConfigGroup,
  parseChoices,
  parseResponseConfig,
  readResponseConfig,
} from './response-type.util';

describe('response-type util', () => {
  let fb: FormBuilder;

  beforeEach(() => {
    fb = TestBed.inject(FormBuilder);
  });

  it('offers every response type the API knows', () => {
    const offered: string[] = [...RESPONSE_TYPES].sort();
    expect(offered).toEqual(
      ['date', 'multiple_choice', 'numeric', 'pass_fail_na', 'rating', 'text', 'yes_no'].sort(),
    );
  });

  it('parses settings leniently', () => {
    expect(parseResponseConfig(null)).toEqual({});
    expect(parseResponseConfig('not json')).toEqual({});
    expect(parseResponseConfig('"a string"')).toEqual({});
    expect(parseResponseConfig('{"unit":"days"}')).toEqual({ unit: 'days' });
  });

  it('splits choices one per line, trimming and dropping blanks', () => {
    expect(parseChoices(' Fully \n\n  Partially\n   \nNot')).toEqual(['Fully', 'Partially', 'Not']);
  });

  it('seeds the form from an existing item', () => {
    const rating = buildResponseConfigGroup(fb, 'rating', '{"ratingScaleId":"scale-1"}');
    const choice = buildResponseConfigGroup(fb, 'multiple_choice', '{"options":["A","B"]}');

    expect(rating.controls.ratingScaleId.value).toBe('scale-1');
    expect(choice.controls.choices.value).toBe('A\nB');
    expect(rating.valid).toBe(true);
    expect(choice.valid).toBe(true);
  });

  it('a new item defaults to Pass / Fail / N/A and is valid with no settings', () => {
    const group = buildResponseConfigGroup(fb, null, null);

    expect(group.controls.responseType.value).toBe('pass_fail_na');
    expect(group.valid).toBe(true);
    expect(readResponseConfig(group)).toEqual({ responseType: 'pass_fail_na', responseConfigJson: null });
  });

  it('a rating is invalid until a scale is chosen', () => {
    const group = buildResponseConfigGroup(fb, 'pass_fail_na', null);

    group.controls.responseType.setValue('rating');
    expect(group.invalid).toBe(true);
    expect(group.controls.ratingScaleId.hasError('required')).toBe(true);

    group.controls.ratingScaleId.setValue('scale-1');
    expect(group.valid).toBe(true);
    expect(readResponseConfig(group)).toEqual({
      responseType: 'rating',
      responseConfigJson: '{"ratingScaleId":"scale-1"}',
    });
  });

  it('a multiple-choice item needs at least two distinct choices', () => {
    const group = buildResponseConfigGroup(fb, 'multiple_choice', null);
    expect(group.controls.choices.hasError('choicesRequired')).toBe(true);

    group.controls.choices.setValue('Only one');
    expect(group.controls.choices.hasError('choicesRequired')).toBe(true);

    group.controls.choices.setValue('Yes\nyes');
    expect(group.controls.choices.getError('duplicateChoice')).toBe('yes');

    group.controls.choices.setValue(' Fully \nPartially\n\nNot');
    expect(group.valid).toBe(true);
    expect(readResponseConfig(group)).toEqual({
      responseType: 'multiple_choice',
      responseConfigJson: '{"options":["Fully","Partially","Not"]}',
    });
  });

  it('settings that belong to another type stop blocking the form once the type changes', () => {
    const group = buildResponseConfigGroup(fb, 'multiple_choice', null);
    expect(group.invalid).toBe(true);

    group.controls.responseType.setValue('date');
    expect(group.valid).toBe(true);
    // …and are not sent: a Date item carries no settings.
    group.controls.choices.setValue('A\nB');
    expect(readResponseConfig(group)).toEqual({ responseType: 'date', responseConfigJson: null });
  });

  it('a number keeps an optional, trimmed unit', () => {
    const group = buildResponseConfigGroup(fb, 'numeric', '{"unit":"days"}');
    expect(readResponseConfig(group).responseConfigJson).toBe('{"unit":"days"}');

    group.controls.unit.setValue('   ');
    expect(readResponseConfig(group).responseConfigJson).toBeNull();

    group.controls.unit.setValue('x'.repeat(31));
    expect(group.controls.unit.hasError('maxlength')).toBe(true);
  });
});
