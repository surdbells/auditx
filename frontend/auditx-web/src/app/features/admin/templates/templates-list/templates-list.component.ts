import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { debounceTime } from 'rxjs';

import { TemplatesService } from '../../../../core/services/templates.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ReferenceDataLookupService } from '../../../../core/services/reference-data-lookup.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Permissions } from '../../../../core/permissions';
import {
  TemplateListItem,
  TemplateStatus,
} from '../../../../core/models';
import {
  CloneTemplateDialogComponent,
  CloneTemplateDialogData,
} from '../dialogs/clone-template-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

@Component({
  selector: 'app-templates-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
  ],
  templateUrl: './templates-list.component.html',
  styleUrl: './templates-list.component.scss',
})
export class TemplatesListComponent {
  private readonly templatesService = inject(TemplatesService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);
  /** Backs the audit-type filter dropdown and column labels (lazy-loaded). */
  readonly refLookup = inject(ReferenceDataLookupService);

  readonly auditTypes = this.refLookup.options('audit_type');

  readonly displayedColumns = [
    'name',
    'auditType',
    'status',
    'currentVersion',
    'itemCount',
    'actions',
  ];

  readonly statuses: { value: TemplateStatus | 'all'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('templatesAdmin.status.all') },
    { value: 'draft', label: this.i18n.translate('templatesAdmin.status.draft') },
    { value: 'published', label: this.i18n.translate('templatesAdmin.status.published') },
    { value: 'archived', label: this.i18n.translate('templatesAdmin.status.archived') },
  ];

  readonly filters = this.fb.nonNullable.group({
    auditType: '',
    status: 'all' as TemplateStatus | 'all',
    search: '',
  });

  readonly state = signal<ViewState>('loading');
  readonly templates = signal<TemplateListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageTemplates),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.templates().length === 0,
  );

  constructor() {
    this.fetchPage(1);

    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    const { auditType, status, search } = this.filters.getRawValue();
    this.templatesService
      .list({ auditType, status, search, page, pageSize: this.pageSize() })
      .subscribe({
        next: (result) => {
          this.templates.set(result.items);
          this.total.set(result.total);
          this.page.set(result.page);
          this.state.set('ready');
          this.loading.set(false);
        },
        error: () => {
          if (this.state() === 'loading') {
            this.state.set('error');
          }
          this.loading.set(false);
        },
      });
  }

  onPageChange(page: number): void {
    this.fetchPage(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.fetchPage(1);
  }

  clearFilters(): void {
    this.filters.reset({ auditType: '', status: 'all', search: '' });
  }

  createTemplate(): void {
    void this.router.navigate(['/admin/templates/new']);
  }

  open(template: TemplateListItem): void {
    void this.router.navigate(['/admin/templates', template.id]);
  }

  clone(template: TemplateListItem, event: Event): void {
    event.stopPropagation();
    const data: CloneTemplateDialogData = { sourceName: template.name };
    this.dialog
      .open(CloneTemplateDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }
        this.templatesService.clone(template.id, result).subscribe({
          next: (created) => {
            this.notify.success(
              this.i18n.translate('templatesAdmin.notify.cloned', {
                name: created.name,
              }),
            );
            void this.router.navigate(['/admin/templates', created.id]);
          },
        });
      });
  }

  archive(template: TemplateListItem, event: Event): void {
    event.stopPropagation();
    const data: ConfirmDialogData = {
      title: this.i18n.translate('templatesAdmin.archive.title'),
      message: this.i18n.translate('templatesAdmin.archive.message', {
        name: template.name,
      }),
      confirmLabel: this.i18n.translate('templatesAdmin.action.archive'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.templatesService.archive(template.id).subscribe({
          next: () => {
            this.notify.success(
              this.i18n.translate('templatesAdmin.notify.archived', {
                name: template.name,
              }),
            );
            this.fetchPage(this.page());
          },
        });
      });
  }

  unarchive(template: TemplateListItem, event: Event): void {
    event.stopPropagation();
    this.templatesService.unarchive(template.id).subscribe({
      next: () => {
        this.notify.success(
          this.i18n.translate('templatesAdmin.notify.restored', {
            name: template.name,
          }),
        );
        this.fetchPage(this.page());
      },
    });
  }
}
