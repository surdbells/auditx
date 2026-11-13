import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { RatingScale, ResponseType } from '../../../core/models';
import {
  MAX_CHOICES,
  MAX_CHOICE_LENGTH,
  MAX_UNIT_LENGTH,
  MIN_CHOICES,
  RESPONSE_TYPES,
  ResponseConfigGroup,
} from '../../../core/models/response-type.util';

/**
 * The "how is this item answered?" block of a checklist-item form: the response type, plus the settings that
 * type needs before an auditor can answer it — a rating scale, the list of choices, or a unit. Shared by the
 * template item editor and the audit checklist-item dialog so both offer every response type identically.
 *
 * Bind it to a group created with `buildResponseConfigGroup(...)`; that group owns the validation.
 */
@Component({
  selector: 'app-response-config-fields',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, TranslatePipe],
  template: `
    <div [formGroup]="group()" class="rcf">
      <mat-form-field appearance="outline" class="rcf__field" subscriptSizing="dynamic">
        <mat-label>{{ 'responseTypes.field.type' | t }}</mat-label>
        <mat-select formControlName="responseType">
          @for (rt of types; track rt) {
            <mat-option [value]="rt">{{ 'audits.responseType.' + rt | t }}</mat-option>
          }
        </mat-select>
        <mat-hint>{{ 'responseTypes.hint.' + type() | t }}</mat-hint>
      </mat-form-field>

      @switch (type()) {
        @case ('rating') {
          <mat-form-field appearance="outline" class="rcf__field" subscriptSizing="dynamic">
            <mat-label>{{ 'responseTypes.field.ratingScale' | t }}</mat-label>
            <mat-select formControlName="ratingScaleId">
              @for (rs of selectableScales(); track rs.id) {
                <mat-option [value]="rs.id">
                  {{ rs.name }}{{ rs.isActive ? '' : ' ' + ('responseTypes.scaleInactive' | t) }}
                </mat-option>
              }
            </mat-select>
            @if (group().controls.ratingScaleId.hasError('required')) {
              <mat-error>{{ 'responseTypes.error.ratingScaleRequired' | t }}</mat-error>
            }
            @if (scalesLoaded() && selectableScales().length === 0) {
              <mat-hint>{{ 'responseTypes.noScales' | t }}</mat-hint>
            } @else {
              <mat-hint>{{ 'responseTypes.field.ratingScaleHint' | t }}</mat-hint>
            }
          </mat-form-field>
        }
        @case ('multiple_choice') {
          <mat-form-field appearance="outline" class="rcf__field" subscriptSizing="dynamic">
            <mat-label>{{ 'responseTypes.field.choices' | t }}</mat-label>
            <textarea
              matInput
              formControlName="choices"
              rows="4"
              [placeholder]="'responseTypes.field.choicesPlaceholder' | t"
            ></textarea>
            @if (group().controls.choices.hasError('choicesRequired')) {
              <mat-error>{{ 'responseTypes.error.choicesRequired' | t: { min: minChoices } }}</mat-error>
            } @else if (group().controls.choices.hasError('duplicateChoice')) {
              <mat-error>
                {{ 'responseTypes.error.duplicateChoice' | t: { choice: group().controls.choices.getError('duplicateChoice') } }}
              </mat-error>
            } @else if (group().controls.choices.hasError('tooManyChoices')) {
              <mat-error>{{ 'responseTypes.error.tooManyChoices' | t: { max: maxChoices } }}</mat-error>
            } @else if (group().controls.choices.hasError('choiceTooLong')) {
              <mat-error>{{ 'responseTypes.error.choiceTooLong' | t: { max: maxChoiceLength } }}</mat-error>
            }
            <mat-hint>{{ 'responseTypes.field.choicesHint' | t }}</mat-hint>
          </mat-form-field>
        }
        @case ('numeric') {
          <mat-form-field appearance="outline" class="rcf__field" subscriptSizing="dynamic">
            <mat-label>{{ 'responseTypes.field.unit' | t }}</mat-label>
            <input
              matInput
              formControlName="unit"
              autocomplete="off"
              [attr.maxlength]="maxUnitLength"
              [placeholder]="'responseTypes.field.unitPlaceholder' | t"
            />
            <mat-hint>{{ 'responseTypes.field.unitHint' | t }}</mat-hint>
          </mat-form-field>
        }
      }
    </div>
  `,
  styles: `
    .rcf {
      display: flex;
      flex-direction: column;
    }
    /* The hint row sizes to its text (some hints wrap), so spacing comes from the field, not a reserved row. */
    .rcf__field {
      width: 100%;
      margin-bottom: 1rem;
    }
  `,
})
export class ResponseConfigFieldsComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  /** The sub-form to edit — create it with `buildResponseConfigGroup`. */
  readonly group = input.required<ResponseConfigGroup>();
  /** Every rating scale (active and inactive); inactive ones are offered only when already selected. */
  readonly ratingScales = input<RatingScale[]>([]);
  /** False while the scales are still loading, so "no scales yet" is not shown prematurely. */
  readonly scalesLoaded = input(true);

  readonly types = RESPONSE_TYPES;
  readonly minChoices = MIN_CHOICES;
  readonly maxChoices = MAX_CHOICES;
  readonly maxChoiceLength = MAX_CHOICE_LENGTH;
  readonly maxUnitLength = MAX_UNIT_LENGTH;

  /** The selected type, mirrored into a signal so the conditional fields re-render under OnPush. */
  readonly type = signal<ResponseType>('pass_fail_na');
  /** The scale the item referenced when the form opened — stays selectable even if it has since been retired. */
  private readonly originalScaleId = signal('');

  /** Active scales, plus the item's original scale even if it is now inactive. */
  readonly selectableScales = computed(() =>
    this.ratingScales().filter((s) => s.isActive || s.id === this.originalScaleId()),
  );

  ngOnInit(): void {
    const controls = this.group().controls;
    this.type.set(controls.responseType.value);
    this.originalScaleId.set(controls.ratingScaleId.value);
    controls.responseType.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((type) => this.type.set(type));
  }
}
