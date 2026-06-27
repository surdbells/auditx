import {
  ChangeDetectionStrategy,
  Component,
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
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

import { NotificationAdminService } from '../../../../core/services/notifications-admin.service';
import { NotificationRule, RulePreview } from '../../../../core/models';

export interface NotificationPreviewDialogData {
  rule: NotificationRule;
}

type PreviewState = 'idle' | 'loading' | 'ready' | 'error';

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
  selector: 'app-notification-preview-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './notification-preview-dialog.component.html',
  styleUrl: './notification-preview-dialog.component.scss',
})
export class NotificationPreviewDialogComponent {
  readonly data = inject<NotificationPreviewDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<NotificationPreviewDialogComponent>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationAdminService);

  readonly state = signal<PreviewState>('idle');
  readonly preview = signal<RulePreview | null>(null);

  readonly form = this.fb.nonNullable.group({
    samplePayloadJson: [
      '{\n  "ownerUserId": "00000000-0000-0000-0000-000000000000"\n}',
      [Validators.required, jsonValidator],
    ],
  });

  run(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.state.set('loading');
    this.preview.set(null);
    this.notifications
      .previewRule({
        recipientResolutionJson: this.data.rule.recipientResolutionJson,
        templateKey: this.data.rule.templateKey,
        samplePayloadJson: this.form.controls.samplePayloadJson.value.trim(),
      })
      .subscribe({
        next: (result) => {
          this.preview.set(result);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  close(): void {
    this.dialogRef.close();
  }
}
