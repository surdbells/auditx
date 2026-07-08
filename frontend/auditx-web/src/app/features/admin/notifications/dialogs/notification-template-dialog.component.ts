import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import {
  NotificationChannel,
  NotificationTemplate,
  NotificationTemplateRequest,
} from '../../../../core/models';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface NotificationTemplateDialogData {
  /** Present when overriding an existing (typically system) template. */
  template?: NotificationTemplate;
}

@Component({
  selector: 'app-notification-template-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    TranslatePipe,
  ],
  templateUrl: './notification-template-dialog.component.html',
  styleUrl: './notification-template-dialog.component.scss',
})
export class NotificationTemplateDialogComponent {
  readonly data = inject<NotificationTemplateDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<
      MatDialogRef<
        NotificationTemplateDialogComponent,
        NotificationTemplateRequest
      >
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly channels: { value: NotificationChannel; label: string }[] = [
    {
      value: 'email',
      label: this.i18n.translate('notifications.channel.email'),
    },
    { value: 'sms', label: this.i18n.translate('notifications.channel.sms') },
  ];
  readonly isOverride = signal(!!this.data.template);

  readonly title = computed(() =>
    this.isOverride()
      ? this.i18n.translate('notifications.templateDialog.title.override')
      : this.i18n.translate('notifications.templates.new'),
  );

  readonly form = this.fb.nonNullable.group({
    templateKey: [this.data.template?.templateKey ?? '', [Validators.required]],
    channel: [
      (this.data.template?.channel as NotificationChannel) ?? 'email',
      [Validators.required],
    ],
    subjectTemplate: [this.data.template?.subjectTemplate ?? ''],
    bodyTemplate: [
      this.data.template?.bodyTemplate ?? '',
      [Validators.required],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      templateKey: v.templateKey.trim(),
      channel: v.channel,
      subjectTemplate: v.subjectTemplate.trim() || null,
      bodyTemplate: v.bodyTemplate,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
