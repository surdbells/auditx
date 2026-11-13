import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';

import { AuditsService } from '../../../core/services/audits.service';
import {
  ChecklistResponse,
  ResponseInput,
  ResponseInputOption,
  ResponseType,
  ResponseVerdict,
  VALUE_RESPONSE_TYPES,
} from '../../../core/models';
import { parseResponseConfig } from '../../../core/models/response-type.util';

export interface RespondItemDialogData {
  /** The audit and item being answered — used to load what the item offers (labels, scale points, choices). */
  auditId: string;
  itemId: string;
  /** The checklist item prompt, for context. */
  prompt: string;
  /** The item's response type — drives which input is shown. */
  responseType: ResponseType;
  /** Per-type config JSON (choice options, rating scale, numeric unit). */
  responseConfigJson?: string | null;
  /** The current draft / submitted response, when one exists. */
  current?: ChecklistResponse | null;
}

export interface RespondItemDialogResult {
  verdict: ResponseVerdict | null;
  /** The chosen organisation-defined conclusion option code, when a custom option set governs the item. */
  selectedOptionCode: string | null;
  comment: string | null;
  /** Auditor's observation — what was found. Optional. */
  observation: string | null;
  /** Auditor's recommendation — suggested corrective action. Optional. */
  recommendation: string | null;
  /** Type-specific captured value, JSON-encoded (null for pure verdict types). */
  valueJson: string | null;
  /** True = Save draft, false = Submit. */
  isDraft: boolean;
}


@Component({
  selector: 'app-respond-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Respond</h2>
    <mat-dialog-content>
      <p class="prompt">{{ data.prompt }}</p>
      <form [formGroup]="form" class="form">
        <!-- Verdict types capture the conclusion directly -->
        @if (!isValueType()) {
          @if (usesOptionSet()) {
            <mat-radio-group formControlName="selectedOptionCode" class="verdicts" aria-label="Conclusion">
              @for (o of options(); track o.code) {
                <mat-radio-button [value]="o.code">{{ o.label }}</mat-radio-button>
              }
            </mat-radio-group>
          } @else {
            <mat-radio-group formControlName="verdict" class="verdicts">
              @if (responseType === 'yes_no') {
                <mat-radio-button value="pass">Yes</mat-radio-button>
                <mat-radio-button value="fail">No</mat-radio-button>
                <mat-radio-button value="na">N/A</mat-radio-button>
              } @else {
                <mat-radio-button value="pass">Pass</mat-radio-button>
                <mat-radio-button value="fail">Fail</mat-radio-button>
                <mat-radio-button value="na">N/A</mat-radio-button>
              }
            </mat-radio-group>
          }
        } @else {
          <!-- Value types capture a typed value -->
          @switch (responseType) {
            @case ('text') {
              <mat-form-field appearance="outline" class="full">
                <mat-label>Response</mat-label>
                <textarea matInput formControlName="value" rows="3"></textarea>
              </mat-form-field>
            }
            @case ('numeric') {
              <mat-form-field appearance="outline" class="full">
                <mat-label>Value{{ unit() ? ' (' + unit() + ')' : '' }}</mat-label>
                <input matInput type="number" step="any" formControlName="value" />
                @if (form.controls.value.value && !isNumber()) {
                  <mat-hint class="warn">Enter a number.</mat-hint>
                }
              </mat-form-field>
            }
            @case ('date') {
              <mat-form-field appearance="outline" class="full">
                <mat-label>Date</mat-label>
                <input matInput type="date" formControlName="value" />
              </mat-form-field>
            }
            @case ('rating') {
              <div class="rating">
                <span class="rating__label">Rating</span>
                <mat-radio-group formControlName="value" class="choices" aria-label="Rating">
                  @for (p of ratingPoints(); track p.value) {
                    <mat-radio-button [value]="p.value.toString()">{{ p.label ? p.value + ' – ' + p.label : p.value }}</mat-radio-button>
                  }
                </mat-radio-group>
              </div>
            }
            @case ('multiple_choice') {
              @if (inputLoaded() && choices().length === 0) {
                <p class="notice" role="alert">
                  This item has no choices to pick from. Ask the audit manager to edit the item and add its choices.
                </p>
              } @else {
                <mat-radio-group formControlName="value" class="choices" aria-label="Choice">
                  @for (opt of choices(); track opt) {
                    <mat-radio-button [value]="opt">{{ opt }}</mat-radio-button>
                  }
                </mat-radio-group>
              }
            }
          }

          <!-- Optional conclusion so any value item can still drive an exception -->
          <div class="conclusion">
            <span class="conclusion__label">Conclusion (optional)</span>
            <mat-radio-group formControlName="verdict" class="verdicts">
              <mat-radio-button value="pass">Pass</mat-radio-button>
              <mat-radio-button value="fail">Fail</mat-radio-button>
              <mat-radio-button value="na">N/A</mat-radio-button>
            </mat-radio-group>
          </div>
        }

        <mat-form-field appearance="outline" class="full">
          <mat-label>Comment{{ commentRequired() ? '' : ' (optional)' }}</mat-label>
          <textarea matInput formControlName="comment" rows="3"></textarea>
          @if (form.hasError('commentRequired') && form.controls.comment.touched) {
            <mat-error>A comment is required for Fail or N/A.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Observation (optional)</mat-label>
          <textarea matInput formControlName="observation" rows="2"></textarea>
          <mat-hint>What was found during the review.</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Recommendation (optional)</mat-label>
          <textarea matInput formControlName="recommendation" rows="2"></textarea>
          <mat-hint>The suggested corrective action.</mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton type="button" (click)="saveDraft()">Save draft</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="!canSubmit()"
      >
        Submit
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .prompt {
      margin: 0 0 1rem;
      font-weight: 500;
    }
    .form {
      display: flex;
      flex-direction: column;
      min-width: 420px;
      gap: 0.75rem;
    }
    .verdicts,
    .choices {
      display: flex;
      gap: 1.25rem;
      flex-wrap: wrap;
    }
    .choices {
      flex-direction: column;
      gap: 0.35rem;
    }
    .rating,
    .conclusion {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
    }
    .rating__label,
    .conclusion__label {
      font-size: 0.78rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .full {
      width: 100%;
    }
    .warn {
      color: var(--mat-sys-error);
    }
    .notice {
      margin: 0;
      padding: 0.6rem 0.8rem;
      border-radius: 10px;
      background: color-mix(in srgb, var(--mat-sys-error) 10%, transparent);
      color: var(--mat-sys-error);
      font-size: 0.875rem;
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class RespondItemDialogComponent {
  readonly data = inject<RespondItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<RespondItemDialogComponent, RespondItemDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly audits = inject(AuditsService);

  readonly responseType = this.data.responseType;
  private readonly config = parseResponseConfig(this.data.responseConfigJson);

  /**
   * What this item offers, resolved by the server from the audit: the organisation's conclusion labels, the
   * rating scale's points, the item's choices, its unit. Null until loaded (or if the load failed — the form then
   * falls back to the built-in conclusions and whatever the item's own settings carry).
   */
  readonly input = signal<ResponseInput | null>(null);
  readonly inputLoaded = signal(false);

  readonly isValueType = computed(() => VALUE_RESPONSE_TYPES.includes(this.responseType));

  /** The conclusion options for a verdict-based item (the org's own labels, or the built-in defaults). */
  readonly options = computed<ResponseInputOption[]>(() => this.input()?.options ?? []);

  /** True once the server's conclusion options are in hand for a verdict-based item. */
  usesOptionSet(): boolean {
    return !this.isValueType() && this.options().length > 0;
  }

  /** A comment is mandatory for a Fail/N-A verdict, or for a chosen option that is a finding / N/A / requires-comment. */
  private readonly commentValidator: ValidatorFn = (group): ValidationErrors | null => {
    const comment = ((group.get('comment')?.value as string) ?? '').trim();
    return this.commentRequired(group) && !comment ? { commentRequired: true } : null;
  };

  readonly form = this.fb.nonNullable.group(
    {
      verdict: [(this.data.current?.verdict ?? null) as ResponseVerdict | null],
      selectedOptionCode: [this.data.current?.selectedOptionCode ?? ''],
      value: [parseValue(this.data.current?.valueJson ?? null)],
      comment: [this.data.current?.comment ?? ''],
      observation: [this.data.current?.observation ?? ''],
      recommendation: [this.data.current?.recommendation ?? ''],
    },
    { validators: [this.commentValidator] },
  );

  constructor() {
    this.audits.getResponseInput(this.data.auditId, this.data.itemId).subscribe({
      next: (input) => {
        this.input.set(input);
        this.inputLoaded.set(true);
        this.adoptExistingVerdict(input.options ?? []);
        this.form.updateValueAndValidity();
      },
      // Keep the form usable on failure: built-in conclusions + the item's own settings.
      error: () => this.inputLoaded.set(true),
    });
  }

  /**
   * A response saved with a bare verdict (before conclusion options existed, or while they could not be loaded)
   * has no option code. Pre-select the option that carries the same conclusion so re-opening it does not look blank.
   */
  private adoptExistingVerdict(options: ResponseInputOption[]): void {
    const verdict = this.form.controls.verdict.value;
    if (this.isValueType() || this.form.controls.selectedOptionCode.value || !verdict) {
      return;
    }
    const match = options.find((o) =>
      verdict === 'na'
        ? o.isNotApplicable
        : verdict === 'fail'
          ? o.isDeficiency && !o.isNotApplicable
          : !o.isDeficiency && !o.isNotApplicable,
    );
    if (match) {
      this.form.controls.selectedOptionCode.setValue(match.code);
    }
  }

  /** The item's choices: the server's resolved list, or the item's own settings until that arrives. */
  choices(): string[] {
    return this.input()?.choices ?? this.config.options ?? [];
  }

  /** The points of the item's rating scale; a bare 1..max only for a legacy item that has no scale. */
  ratingPoints(): { value: number; label: string }[] {
    const points = this.input()?.ratingPoints;
    if (points && points.length > 0) {
      return points;
    }
    const max = this.config.max ?? 5;
    return Array.from({ length: max }, (_, i) => ({ value: i + 1, label: '' }));
  }

  unit(): string {
    return this.input()?.unit ?? this.config.unit ?? '';
  }

  /** True when the numeric box holds a real (finite) number. */
  isNumber(): boolean {
    const raw = String(this.form.controls.value.value ?? '').trim();
    return raw.length > 0 && Number.isFinite(Number(raw));
  }

  /**
   * True when the chosen conclusion makes a comment mandatory. Reads from the passed `group` (defaulting to the
   * form) so it is safe to call from the group-level validator, which Angular runs while `fb.group()` is still
   * executing — at which point `this.form` is not yet assigned.
   */
  commentRequired(group: AbstractControl = this.form): boolean {
    if (this.usesOptionSet()) {
      const code = group.get('selectedOptionCode')?.value as string | undefined;
      const o = this.options().find((opt) => opt.code === code) ?? null;
      return !!o && (o.requiresComment || o.isDeficiency || o.isNotApplicable);
    }
    const v = group.get('verdict')?.value as ResponseVerdict | null;
    return v === 'fail' || v === 'na';
  }

  /** Value types need a value; option-driven types need a chosen option; plain verdict types need a verdict. */
  canSubmit(): boolean {
    if (this.form.hasError('commentRequired')) {
      return false;
    }
    if (this.isValueType()) {
      const value = String(this.form.controls.value.value ?? '').trim();
      if (this.responseType === 'numeric') {
        return this.isNumber();
      }
      if (this.responseType === 'multiple_choice') {
        return this.choices().includes(value);
      }
      return value.length > 0;
    }
    if (this.usesOptionSet()) {
      return !!this.form.controls.selectedOptionCode.value;
    }
    return !!this.form.controls.verdict.value;
  }

  saveDraft(): void {
    if (this.form.hasError('commentRequired')) {
      this.form.markAllAsTouched();
      return;
    }
    this.close(true);
  }

  submit(): void {
    if (!this.canSubmit()) {
      this.form.markAllAsTouched();
      return;
    }
    this.close(false);
  }

  private close(isDraft: boolean): void {
    const v = this.form.getRawValue();
    const usingOptions = this.usesOptionSet();
    this.dialogRef.close({
      // When an option set is in use the server derives the canonical verdict from the chosen option.
      verdict: usingOptions ? null : v.verdict,
      selectedOptionCode: usingOptions ? v.selectedOptionCode || null : null,
      comment: v.comment.trim() || null,
      observation: v.observation.trim() || null,
      recommendation: v.recommendation.trim() || null,
      valueJson: this.isValueType() ? buildValue(this.responseType, String(v.value ?? '')) : null,
      isDraft,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}

/** Extract the raw scalar from a stored value JSON for seeding the form. */
function parseValue(json: string | null): string {
  if (!json) {
    return '';
  }
  try {
    const v = JSON.parse(json) as Record<string, unknown>;
    const raw = v['text'] ?? v['number'] ?? v['date'] ?? v['rating'] ?? v['choice'];
    return raw === undefined || raw === null ? '' : String(raw);
  } catch {
    return '';
  }
}

/** Serialise the form's raw value into the type-tagged value JSON. */
function buildValue(type: ResponseType, raw: string): string | null {
  const trimmed = raw.trim();
  if (!trimmed) {
    return null;
  }
  switch (type) {
    case 'text':
      return JSON.stringify({ text: trimmed });
    case 'numeric':
      return Number.isFinite(Number(trimmed)) ? JSON.stringify({ number: Number(trimmed) }) : null;
    case 'date':
      return JSON.stringify({ date: trimmed });
    case 'rating':
      return JSON.stringify({ rating: Number(trimmed) });
    case 'multiple_choice':
      return JSON.stringify({ choice: trimmed });
    default:
      return null;
  }
}
