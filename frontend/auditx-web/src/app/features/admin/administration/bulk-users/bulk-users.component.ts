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
import { IconComponent } from '../../../../core/icons/icon.component';
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
    'administration.bulkUsers.guide.action.upload',
    'administration.bulkUsers.guide.action.review',
  ],
  sections: [
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
    IconComponent,
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

  readonly importing = signal(false);
  readonly importResult = signal<BulkOperationResult | null>(null);

  readonly importForm = this.fb.nonNullable.group({
    csvContent: ['', Validators.required],
  });

  /** Download a ready-to-fill CSV template with the exact columns the importer expects. */
  downloadTemplate(): void {
    const csv =
      'email,first_name,last_name,roles\n' +
      'jane.doe@bank.local,Jane,Doe,\n' +
      'john.smith@bank.local,John,Smith,AuditX Administrator\n';
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'auditx-users-template.csv';
    link.click();
    URL.revokeObjectURL(url);
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
