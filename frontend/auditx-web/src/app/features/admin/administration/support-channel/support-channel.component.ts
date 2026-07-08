import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

import { AdministrationService } from '../../../../core/services/administration.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { SupportChannelStatus } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-support-channel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatChipsModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
  ],
  templateUrl: './support-channel.component.html',
  styleUrl: './support-channel.component.scss',
})
export class SupportChannelComponent {
  private readonly admin = inject(AdministrationService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly state = signal<ViewState>('loading');
  readonly status = signal<SupportChannelStatus | null>(null);
  readonly engineers = signal<string[]>([]);
  readonly submitting = signal(false);

  readonly form = this.fb.nonNullable.group({
    durationMinutes: [
      60,
      [Validators.required, Validators.min(1), Validators.max(1440)],
    ],
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.admin.getSupportChannel().subscribe({
      next: (s) => {
        this.status.set(s);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  addEngineer(event: MatChipInputEvent): void {
    const value = (event.value ?? '').trim();
    if (value && !this.engineers().includes(value)) {
      this.engineers.update((list) => [...list, value]);
    }
    event.chipInput?.clear();
  }

  removeEngineer(id: string): void {
    this.engineers.update((list) => list.filter((e) => e !== id));
  }

  enable(): void {
    if (this.form.invalid || this.engineers().length === 0) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.admin
      .enableSupportChannel({
        engineerIdentifiers: this.engineers(),
        durationMinutes: this.form.controls.durationMinutes.value,
      })
      .subscribe({
        next: (s) => {
          this.status.set(s);
          this.engineers.set([]);
          this.notify.success(
            this.i18n.translate('administration.support.enabledToast'),
          );
          this.submitting.set(false);
        },
        error: () => this.submitting.set(false),
      });
  }

  revoke(): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('administration.support.revokeTitle'),
      message: this.i18n.translate('administration.support.revokeMessage'),
      confirmLabel: this.i18n.translate('administration.support.revokeConfirm'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.admin.revokeSupportChannel().subscribe({
          next: () => {
            this.notify.success(
              this.i18n.translate('administration.support.revokedToast'),
            );
            this.fetch();
          },
        });
      });
  }
}
