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

export interface NotificationTemplateDialogData {
  /** Present when overriding an existing (typically system) template. */
  template?: NotificationTemplate;
}

const CHANNELS: { value: NotificationChannel; label: string }[] = [
  { value: 'email', label: 'Email' },
  { value: 'sms', label: 'SMS' },
];

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

  readonly channels = CHANNELS;
  readonly isOverride = signal(!!this.data.template);

  readonly title = computed(() =>
    this.isOverride() ? 'Override template' : 'New bank template',
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
