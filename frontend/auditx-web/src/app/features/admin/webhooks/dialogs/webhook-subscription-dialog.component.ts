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
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { SelectAllDirective } from '../../../../shared/directives/select-all.directive';

import { CreateWebhookSubscriptionRequest, WebhookEventType } from '../../../../core/models';
import { WebhooksService } from '../../../../core/services/webhooks.service';
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
    MatSelectModule,
    SelectAllDirective,
    MatButtonModule,
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
  private readonly webhooks = inject(WebhooksService);

  /** The catalogue of subscribable event types for the dropdown. */
  readonly eventTypeOptions = signal<WebhookEventType[]>([]);

  readonly form = this.fb.nonNullable.group({
    destinationUrl: [
      '',
      [Validators.required, Validators.pattern(/^https?:\/\/.+/)],
    ],
    subscribedEventTypes: [[] as string[], Validators.required],
    hmacSecret: ['', Validators.required],
    retryPolicyJson: ['', [jsonValidator]],
  });

  constructor() {
    this.webhooks.eventTypes().subscribe({
      next: (types) => this.eventTypeOptions.set(types),
      error: () => this.eventTypeOptions.set([]),
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      destinationUrl: v.destinationUrl.trim(),
      subscribedEventTypes: v.subscribedEventTypes,
      hmacSecret: v.hmacSecret,
      retryPolicyJson: v.retryPolicyJson.trim() || '{}',
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
