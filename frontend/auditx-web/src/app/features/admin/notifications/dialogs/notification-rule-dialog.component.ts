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
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import {
  CreateNotificationRuleRequest,
  NotificationChannel,
  NotificationRule,
  UpdateNotificationRuleRequest,
} from '../../../../core/models';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

/** Selectable delivery channels (mirrors CHANNELS in the template dialog). */
const CHANNELS: NotificationChannel[] = ['email', 'sms'];

/**
 * Parses a stored channels JSON-array string into a channel-value array.
 * Tolerates a malformed / empty string by returning an empty selection.
 */
function parseChannels(json: string | null | undefined): NotificationChannel[] {
  if (!json) {
    return [];
  }
  try {
    const parsed = JSON.parse(json);
    if (!Array.isArray(parsed)) {
      return [];
    }
    return parsed.filter((c): c is NotificationChannel =>
      (CHANNELS as string[]).includes(c),
    );
  } catch {
    return [];
  }
}

export interface NotificationRuleDialogData {
  /** Present when editing; absent for create. */
  rule?: NotificationRule;
  /** Event-type keys sourced from `/admin/events/catalogue`. */
  eventTypes: string[];
}

/** Result emitted by the dialog: either a create or an update request. */
export type NotificationRuleDialogResult =
  | { mode: 'create'; body: CreateNotificationRuleRequest }
  | { mode: 'update'; id: string; body: UpdateNotificationRuleRequest };

function jsonValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '').trim();
  if (!value) {
    return null;
  }
  try {
    JSON.parse(value);
    return null;
  } catch {
    return { json: true };
  }
}

@Component({
  selector: 'app-notification-rule-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
  ],
  templateUrl: './notification-rule-dialog.component.html',
  styleUrl: './notification-rule-dialog.component.scss',
})
export class NotificationRuleDialogComponent {
  readonly data = inject<NotificationRuleDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<
      MatDialogRef<
        NotificationRuleDialogComponent,
        NotificationRuleDialogResult
      >
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly isEdit = signal(!!this.data.rule);
  readonly eventTypes = signal<string[]>(this.data.eventTypes ?? []);
  readonly channels = CHANNELS;

  readonly title = computed(() =>
    this.isEdit()
      ? this.i18n.translate('notifications.ruleDialog.title.edit')
      : this.i18n.translate('notifications.ruleDialog.title.new'),
  );

  readonly form = this.fb.nonNullable.group({
    eventType: [this.data.rule?.eventType ?? '', [Validators.required]],
    name: [this.data.rule?.name ?? '', [Validators.required]],
    templateKey: [this.data.rule?.templateKey ?? '', [Validators.required]],
    recipientResolutionJson: [
      this.data.rule?.recipientResolutionJson ?? '',
      [Validators.required, jsonValidator],
    ],
    channels: [
      parseChannels(this.data.rule?.channelsJson),
      [Validators.required, Validators.minLength(1)],
    ],
    isActive: [this.data.rule?.isActive ?? true],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const channelsJson = JSON.stringify(v.channels);
    if (this.isEdit() && this.data.rule) {
      this.dialogRef.close({
        mode: 'update',
        id: this.data.rule.id,
        body: {
          name: v.name.trim(),
          recipientResolutionJson: v.recipientResolutionJson.trim(),
          channelsJson,
          templateKey: v.templateKey.trim(),
          isActive: v.isActive,
          version: this.data.rule.version,
        },
      });
      return;
    }
    this.dialogRef.close({
      mode: 'create',
      body: {
        eventType: v.eventType.trim(),
        name: v.name.trim(),
        recipientResolutionJson: v.recipientResolutionJson.trim(),
        channelsJson,
        templateKey: v.templateKey.trim(),
        isActive: v.isActive,
      },
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
