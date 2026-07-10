import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';

import { AdministrationService } from '../../../../core/services/administration.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { BulkOperationResult } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

/** Contextual page guide for bulk user provisioning (walkthrough + About panel). */
const BULK_USERS_GUIDE: PageGuide = {
  id: 'admin-bulk-users',
  titleKey: 'administration.tabs.bulkUsers',
  purposeKey: 'administration.bulkUsers.guide.purpose',
  descriptionKey: 'administration.bulkUsers.guide.description',
  actionKeys: [
    'administration.bulkUsers.guide.action.import',
    'administration.bulkUsers.guide.action.deactivate',
    'administration.bulkUsers.guide.action.upload',
    'administration.bulkUsers.guide.action.review',
  ],
  sections: [
    { selector: '[data-guide="deactivate"]', titleKey: 'administration.bulkUsers.guide.section.deactivate.title', bodyKey: 'administration.bulkUsers.guide.section.deactivate.body' },
    { selector: '[data-guide="import"]', titleKey: 'administration.bulkUsers.guide.section.import.title', bodyKey: 'administration.bulkUsers.guide.section.import.body' },
    { selector: '.bulk__upload', titleKey: 'administration.bulkUsers.guide.section.upload.title', bodyKey: 'administration.bulkUsers.guide.section.upload.body' },
  ],
  workflowKeys: [
    'administration.bulkUsers.guide.flow.prepare',
    'administration.bulkUsers.guide.flow.upload',
    'administration.bulkUsers.guide.flow.provision',
    'administration.bulkUsers.guide.flow.review',
    'administration.bulkUsers.guide.flow.onboard',
  ],
  dependsOnKeys: [
    'administration.bulkUsers.guide.dep.csv',
    'administration.bulkUsers.guide.dep.roles',
    'administration.bulkUsers.guide.dep.users',
  ],
  usedByKeys: [
    'administration.bulkUsers.guide.use.users',
    'administration.bulkUsers.guide.use.audits',
    'administration.bulkUsers.guide.use.access',
  ],
  businessRuleKeys: [
    'administration.bulkUsers.guide.rule.format',
    'administration.bulkUsers.guide.rule.duplicate',
    'administration.bulkUsers.guide.rule.partial',
    'administration.bulkUsers.guide.rule.deactivate',
  ],
  tipKeys: [
    'administration.bulkUsers.guide.tip.small',
    'administration.bulkUsers.guide.tip.errors',
    'administration.bulkUsers.guide.tip.ids',
  ],
  permissionKeys: [
    'administration.bulkUsers.guide.perm.admin',
    'administration.bulkUsers.guide.perm.manage',
  ],
  faq: [
    { questionKey: 'administration.bulkUsers.guide.faq.format.q', answerKey: 'administration.bulkUsers.guide.faq.format.a' },
    { questionKey: 'administration.bulkUsers.guide.faq.undo.q', answerKey: 'administration.bulkUsers.guide.faq.undo.a' },
  ],
};

@Component({
  selector: 'app-bulk-users',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    TranslatePipe,
    PageGuideComponent,
  ],
  templateUrl: './bulk-users.component.html',
  styleUrl: './bulk-users.component.scss',
})
export class BulkUsersComponent {
  readonly guide = BULK_USERS_GUIDE;

  private readonly admin = inject(AdministrationService);
  private readonly notify = inject(NotificationService);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly errorColumns = ['identifier', 'message'];

  readonly deactivating = signal(false);
  readonly importing = signal(false);
  readonly deactivateResult = signal<BulkOperationResult | null>(null);
  readonly importResult = signal<BulkOperationResult | null>(null);

  readonly deactivateForm = this.fb.nonNullable.group({
    userIds: ['', Validators.required],
  });

  readonly importForm = this.fb.nonNullable.group({
    csvContent: ['', Validators.required],
  });

  private parseIds(raw: string): string[] {
    return raw
      .split(/[\s,;]+/)
      .map((s) => s.trim())
      .filter((s) => s.length > 0);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      this.importForm.controls.csvContent.setValue(String(reader.result ?? ''));
    };
    reader.readAsText(file);
    input.value = '';
  }

  bulkDeactivate(): void {
    const ids = this.parseIds(this.deactivateForm.controls.userIds.value);
    if (ids.length === 0) {
      this.deactivateForm.controls.userIds.markAsTouched();
      return;
    }
    this.deactivating.set(true);
    this.deactivateResult.set(null);
    this.admin.bulkDeactivateUsers(ids).subscribe({
      next: (result) => {
        this.deactivateResult.set(result);
        this.notify.success(
          this.i18n.translate('administration.bulkUsers.notify.deactivated', {
            count: result.successCount,
          }),
        );
        this.deactivating.set(false);
      },
      error: () => this.deactivating.set(false),
    });
  }

  bulkImport(): void {
    if (this.importForm.invalid) {
      this.importForm.markAllAsTouched();
      return;
    }
    this.importing.set(true);
    this.importResult.set(null);
    this.admin
      .bulkImportUsers(this.importForm.controls.csvContent.value)
      .subscribe({
        next: (result) => {
          this.importResult.set(result);
          this.notify.success(
            this.i18n.translate('administration.bulkUsers.notify.imported', {
              count: result.successCount,
            }),
          );
          this.importing.set(false);
        },
        error: () => this.importing.set(false),
      });
  }
}
