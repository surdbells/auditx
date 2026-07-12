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
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';

import { CreateWebhookSubscriptionRequest } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

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
  selector: 'app-webhook-subscription-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatChipsModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
  ],
  templateUrl: './webhook-subscription-dialog.component.html',
  styleUrl: './webhook-subscription-dialog.component.scss',
})
export class WebhookSubscriptionDialogComponent {
  private readonly dialogRef =
    inject<
      MatDialogRef<
        WebhookSubscriptionDialogComponent,
        CreateWebhookSubscriptionRequest
      >
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly eventTypes = signal<string[]>([]);

  readonly form = this.fb.nonNullable.group({
    destinationUrl: [
      '',
      [Validators.required, Validators.pattern(/^https?:\/\/.+/)],
    ],
    hmacSecret: ['', Validators.required],
    retryPolicyJson: ['', [jsonValidator]],
  });

  addEventType(event: MatChipInputEvent): void {
    const value = (event.value ?? '').trim();
    if (value && !this.eventTypes().includes(value)) {
      this.eventTypes.update((list) => [...list, value]);
    }
    event.chipInput?.clear();
  }

  removeEventType(type: string): void {
    this.eventTypes.update((list) => list.filter((t) => t !== type));
  }

  submit(): void {
    if (this.form.invalid || this.eventTypes().length === 0) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      destinationUrl: v.destinationUrl.trim(),
      subscribedEventTypes: this.eventTypes(),
      hmacSecret: v.hmacSecret,
      retryPolicyJson: v.retryPolicyJson.trim() || '{}',
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
