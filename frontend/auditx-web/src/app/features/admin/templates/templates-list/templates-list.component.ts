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

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 20;

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
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageTemplates),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.templates().length === 0,
  );

  constructor() {
    this.fetchFirstPage();

    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchFirstPage());
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.templates.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.templates.set(items);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.state.set('ready');
    });
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.query(this.nextCursor(), (items, cursor, more) => {
      this.templates.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: TemplateListItem[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    const { auditType, status, search } = this.filters.getRawValue();
    this.templatesService
      .list({ auditType, status, search, cursor, limit: PAGE_SIZE })
      .subscribe({
        next: (page) => onSuccess(page.items, page.nextCursor, page.hasMore),
        error: () => {
          if (cursor === null) {
            this.state.set('error');
          } else {
            this.loadingMore.set(false);
          }
        },
      });
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
            this.fetchFirstPage();
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
        this.fetchFirstPage();
      },
    });
  }
}
