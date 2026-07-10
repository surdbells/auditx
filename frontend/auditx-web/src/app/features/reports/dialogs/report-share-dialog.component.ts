import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';

import { SharedLinksService } from '../../../core/services/shared-links.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SharedLink } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

export interface ReportShareDialogData {
  reportId: string;
}

/**
 * Manages shareable links for a report (D3-B): lists existing links (copy / revoke) and creates a new one with an
 * optional expiry. The shared URL is an internal deep link (/s/{slug}) that still enforces the recipient's own
 * ViewReport permission — it never grants access.
 */
@Component({
  selector: 'app-report-share-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  templateUrl: './report-share-dialog.component.html',
  styleUrl: './report-share-dialog.component.scss',
})
export class ReportShareDialogComponent {
  readonly data = inject<ReportShareDialogData>(MAT_DIALOG_DATA);
  private readonly service = inject(SharedLinksService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);
  private readonly fb = inject(FormBuilder);

  readonly links = signal<SharedLink[]>([]);
  readonly loading = signal(true);
  readonly creating = signal(false);

  /** Expiry options in days; 0 = never. */
  readonly expiryOptions = [
    { value: 0, labelKey: 'reports.share.expiry.never' },
    { value: 7, labelKey: 'reports.share.expiry.d7' },
    { value: 30, labelKey: 'reports.share.expiry.d30' },
    { value: 90, labelKey: 'reports.share.expiry.d90' },
  ];

  readonly form = this.fb.nonNullable.group({ expiresInDays: [0] });

  constructor() {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.service.listForReport(this.data.reportId).subscribe({
      next: (rows) => {
        this.links.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  linkUrl(link: SharedLink): string {
    return `${window.location.origin}/s/${link.slug}`;
  }

  create(): void {
    if (this.creating()) {
      return;
    }
    this.creating.set(true);
    const days = this.form.getRawValue().expiresInDays;
    this.service.createForReport(this.data.reportId, days > 0 ? days : null).subscribe({
      next: (link) => {
        this.creating.set(false);
        void this.copy(link);
        this.refresh();
      },
      error: () => this.creating.set(false),
    });
  }

  async copy(link: SharedLink): Promise<void> {
    const url = this.linkUrl(link);
    try {
      await navigator.clipboard.writeText(url);
      this.notify.success(this.i18n.translate('reports.share.notify.copied'));
    } catch {
      // Clipboard blocked (e.g. insecure context): surface the URL so the user can copy it manually.
      this.notify.info(url);
    }
  }

  revoke(link: SharedLink): void {
    this.service.revoke(link.id, link.version).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('reports.share.notify.revoked'));
        this.refresh();
      },
    });
  }
}
