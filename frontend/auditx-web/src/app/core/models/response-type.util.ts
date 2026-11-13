import {
  AbstractControl,
  FormBuilder,
  FormControl,
  FormGroup,
  ValidationErrors,
  Validators,
} from '@angular/forms';

import type { ResponseType } from './template.models';

/**
 * Everything the app knows about checklist response types in one place, so the template item editor and the
 * audit checklist-item dialog offer the same types with the same per-type settings — and save them in the one
 * shape the API accepts.
 */

/** Every response type, in the order they are offered. */
export const RESPONSE_TYPES: readonly ResponseType[] = [
  'pass_fail_na',
  'yes_no',
  'rating',
  'multiple_choice',
  'numeric',
  'date',
  'text',
];

/** Limits mirrored from the API (`ResponseItemConfigRules`) so the form rejects what the server would. */
export const MIN_CHOICES = 2;
export const MAX_CHOICES = 50;
export const MAX_CHOICE_LENGTH = 200;
export const MAX_UNIT_LENGTH = 30;

/** i18n key for a response type's display name. */
export function responseTypeLabelKey(type: ResponseType | string): string {
  return `audits.responseType.${type}`;
}

/** The per-type settings carried in an item's `responseConfigJson`. */
export interface ResponseItemConfig {
  /** Rating: the scale the item is answered (and scored) on. */
  ratingScaleId?: string;
  /** Multiple choice: the choices offered. */
  options?: string[];
  /** Numeric: the unit shown beside the value. */
  unit?: string;
  /** Legacy rating shape (1..max) from before rating scales — read only. */
  max?: number;
}

export function parseResponseConfig(json: string | null | undefined): ResponseItemConfig {
  if (!json) {
    return {};
  }
  try {
    const parsed = JSON.parse(json) as unknown;
    return parsed && typeof parsed === 'object' ? (parsed as ResponseItemConfig) : {};
  } catch {
    return {};
  }
}

/** Split the "one per line" choices box into trimmed, non-empty choices. */
export function parseChoices(text: string): string[] {
  return text
    .split('\n')
    .map((c) => c.trim())
    .filter((c) => c.length > 0);
}

/** The controls that capture a response type and its settings. */
export interface ResponseConfigControls {
  responseType: FormControl<ResponseType>;
  ratingScaleId: FormControl<string>;
  choices: FormControl<string>;
  unit: FormControl<string>;
}

export type ResponseConfigGroup = FormGroup<ResponseConfigControls>;

/** At least two, at most fifty, distinct choices of a sensible length. */
function choicesValidator(control: AbstractControl): ValidationErrors | null {
  const choices = parseChoices((control.value as string) ?? '');
  if (choices.length < MIN_CHOICES) {
    return { choicesRequired: true };
  }
  if (choices.length > MAX_CHOICES) {
    return { tooManyChoices: true };
  }
  if (choices.some((c) => c.length > MAX_CHOICE_LENGTH)) {
    return { choiceTooLong: true };
  }
  const seen = new Set<string>();
  for (const c of choices) {
    const key = c.toLowerCase();
    if (seen.has(key)) {
      return { duplicateChoice: c };
    }
    seen.add(key);
  }
  return null;
}

/**
 * Build the response-type sub-form, seeded from an existing item. Validators follow the selected type: a
 * rating needs a scale, a multiple-choice item needs its choices — so the parent form is invalid (and its
 * Save button disabled) until the item is one an auditor can actually answer.
 */
export function buildResponseConfigGroup(
  fb: FormBuilder,
  responseType: ResponseType | null | undefined,
  responseConfigJson: string | null | undefined,
): ResponseConfigGroup {
  const config = parseResponseConfig(responseConfigJson);
  const group = fb.nonNullable.group({
    responseType: [(responseType ?? 'pass_fail_na') as ResponseType, [Validators.required]],
    ratingScaleId: [config.ratingScaleId ?? ''],
    choices: [(config.options ?? []).join('\n')],
    unit: [config.unit ?? '', [Validators.maxLength(MAX_UNIT_LENGTH)]],
  });

  const sync = (type: ResponseType): void => {
    group.controls.ratingScaleId.setValidators(type === 'rating' ? [Validators.required] : []);
    group.controls.choices.setValidators(type === 'multiple_choice' ? [choicesValidator] : []);
    group.controls.ratingScaleId.updateValueAndValidity({ emitEvent: false });
    group.controls.choices.updateValueAndValidity({ emitEvent: false });
  };
  sync(group.controls.responseType.value);
  // The subscription lives exactly as long as the form it belongs to.
  group.controls.responseType.valueChanges.subscribe(sync);
  return group;
}

/** The `{ responseType, responseConfigJson }` pair to send to the API for the sub-form's current value. */
export function readResponseConfig(group: ResponseConfigGroup): {
  responseType: ResponseType;
  responseConfigJson: string | null;
} {
  const v = group.getRawValue();
  switch (v.responseType) {
    case 'rating':
      return {
        responseType: v.responseType,
        responseConfigJson: v.ratingScaleId ? JSON.stringify({ ratingScaleId: v.ratingScaleId }) : null,
      };
    case 'multiple_choice':
      return {
        responseType: v.responseType,
        responseConfigJson: JSON.stringify({ options: parseChoices(v.choices) }),
      };
    case 'numeric': {
      const unit = v.unit.trim();
      return { responseType: v.responseType, responseConfigJson: unit ? JSON.stringify({ unit }) : null };
    }
    default:
      return { responseType: v.responseType, responseConfigJson: null };
  }
}
