import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
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

import {
  ChecklistResponse,
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
  comment: string | null;
  /** Type-specific captured value, JSON-encoded (null for pure verdict types). */
  valueJson: string | null;
  /** True = Save draft, false = Submit. */
  isDraft: boolean;
}

/** A comment is required when the verdict is Fail or N/A (runs on the group to read the sibling verdict). */
const failNaNeedsComment: ValidatorFn = (group): ValidationErrors | null => {
  const verdict = group.get('verdict')?.value as ResponseVerdict | null;
  const comment = ((group.get('comment')?.value as string) ?? '').trim();
  if ((verdict === 'fail' || verdict === 'na') && !comment) {
    return { commentRequired: true };
  }
  return null;
};

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
                  @for (n of ratingScale(); track n) {
                    <mat-radio-button [value]="n.toString()">{{ n }}</mat-radio-button>
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

  readonly responseType = this.data.responseType;
  private readonly config = parseConfig(this.data.responseConfigJson ?? null);

  readonly isValueType = computed(() => VALUE_RESPONSE_TYPES.includes(this.responseType));

  readonly form = this.fb.nonNullable.group(
    {
      verdict: [(this.data.current?.verdict ?? null) as ResponseVerdict | null],
      value: [parseValue(this.data.current?.valueJson ?? null)],
      comment: [this.data.current?.comment ?? ''],
    },
    { validators: [failNaNeedsComment] },
  );

  choices(): string[] {
    return this.config.options ?? [];
  }

  ratingScale(): number[] {
    const max = this.config.max ?? 5;
    return Array.from({ length: max }, (_, i) => i + 1);
  }

  unit(): string {
    return this.config.unit ?? '';
  }

  /** True when the chosen verdict makes a comment mandatory. */
  commentRequired(): boolean {
    const v = this.form.controls.verdict.value;
    return v === 'fail' || v === 'na';
  }

  /** Value types need a value to submit; verdict types need a verdict. */
  canSubmit(): boolean {
    if (this.form.hasError('commentRequired')) {
      return false;
    }
    if (this.isValueType()) {
      return this.form.controls.value.value.trim().length > 0;
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
    this.dialogRef.close({
      verdict: v.verdict,
      comment: v.comment.trim() || null,
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
