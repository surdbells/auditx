import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
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

import { RatingScalesService } from '../../../core/services/rating-scales.service';
import { ResponseOptionSetsService } from '../../../core/services/response-option-sets.service';
import {
  ChecklistResponse,
  RatingScalePoint,
  ResponseOption,
  ResponseOptionSet,
  ResponseType,
  ResponseVerdict,
  VALUE_RESPONSE_TYPES,
} from '../../../core/models';

export interface RespondItemDialogData {
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
            <mat-radio-group formControlName="selectedOptionCode" class="choices">
              @for (o of optionSet()!.options; track o.code) {
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
                <input matInput type="number" formControlName="value" />
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
                <mat-radio-group formControlName="value" class="verdicts">
                  @for (p of ratingScale(); track p.value) {
                    <mat-radio-button [value]="p.value.toString()">{{ p.label ? p.value + ' – ' + p.label : p.value }}</mat-radio-button>
                  }
                </mat-radio-group>
              </div>
            }
            @case ('multiple_choice') {
              <mat-radio-group formControlName="value" class="choices">
                @for (opt of choices(); track opt) {
                  <mat-radio-button [value]="opt">{{ opt }}</mat-radio-button>
                }
              </mat-radio-group>
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
  private readonly ratingScalesService = inject(RatingScalesService);
  private readonly responseOptionSets = inject(ResponseOptionSetsService);

  readonly responseType = this.data.responseType;
  private readonly config = parseConfig(this.data.responseConfigJson ?? null);
  private readonly ratingScalePoints = signal<RatingScalePoint[] | null>(null);
  /** The org-defined conclusion options for this verdict-based type, once loaded. */
  readonly optionSet = signal<ResponseOptionSet | null>(null);

  readonly isValueType = computed(() => VALUE_RESPONSE_TYPES.includes(this.responseType));

  /** True when a custom option set governs this item (verdict type with configured options). */
  usesOptionSet(): boolean {
    return !this.isValueType() && (this.optionSet()?.options.length ?? 0) > 0;
  }

  /** A comment is mandatory for a Fail/N-A verdict, or for a chosen option that is a finding / N/A / requires-comment. */
  private readonly commentValidator: ValidatorFn = (group): ValidationErrors | null => {
    const comment = ((group.get('comment')?.value as string) ?? '').trim();
    return this.commentRequired() && !comment ? { commentRequired: true } : null;
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
    if (this.responseType === 'rating' && this.config.ratingScaleId) {
      const scaleId = this.config.ratingScaleId;
      this.ratingScalesService.list('all').subscribe((scales) => {
        const scale = scales.find((s) => s.id === scaleId);
        if (scale) {
          this.ratingScalePoints.set(parsePoints(scale.pointsJson));
        }
      });
    }

    // Verdict-based types can be governed by an organisation-defined option set; load it so the auditor picks
    // the org's own labels. On failure we silently keep the built-in Pass/Fail/N-A radios.
    if (!this.isValueType()) {
      this.responseOptionSets.get(this.responseType).subscribe({
        next: (set) => {
          this.optionSet.set(set);
          this.form.controls.comment.updateValueAndValidity();
        },
        error: () => this.optionSet.set(null),
      });
    }
  }

  /** The option the auditor has currently selected, when an option set is in use. */
  private selectedOption(): ResponseOption | null {
    const code = this.form.controls.selectedOptionCode.value;
    return this.optionSet()?.options.find((o) => o.code === code) ?? null;
  }

  choices(): string[] {
    return this.config.options ?? [];
  }

  /** Labelled scale points when a RatingScale is configured; a bare 1..max fallback otherwise. */
  ratingScale(): RatingScalePoint[] {
    const points = this.ratingScalePoints();
    if (points) {
      return points;
    }
    const max = this.config.max ?? 5;
    return Array.from({ length: max }, (_, i) => ({ value: i + 1, label: '', score: 0 }));
  }

  unit(): string {
    return this.config.unit ?? '';
  }

  /** True when the chosen conclusion makes a comment mandatory. */
  commentRequired(): boolean {
    if (this.usesOptionSet()) {
      const o = this.selectedOption();
      return !!o && (o.requiresComment || o.isDeficiency || o.isNotApplicable);
    }
    const v = this.form.controls.verdict.value;
    return v === 'fail' || v === 'na';
  }

  /** Value types need a value; option-driven types need a chosen option; plain verdict types need a verdict. */
  canSubmit(): boolean {
    if (this.form.hasError('commentRequired')) {
      return false;
    }
    if (this.isValueType()) {
      return this.form.controls.value.value.trim().length > 0;
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
      valueJson: this.isValueType() ? buildValue(this.responseType, v.value) : null,
      isDraft,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}

interface ParsedConfig {
  options?: string[];
  max?: number;
  unit?: string;
  ratingScaleId?: string;
}

function parseConfig(json: string | null): ParsedConfig {
  if (!json) {
    return {};
  }
  try {
    return JSON.parse(json) as ParsedConfig;
  } catch {
    return {};
  }
}

function parsePoints(pointsJson: string): RatingScalePoint[] {
  try {
    const parsed = JSON.parse(pointsJson);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
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
      return JSON.stringify({ number: Number(trimmed) });
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
