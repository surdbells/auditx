import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';

import { RegulationsService } from '../../../core/services/regulations.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { MatTableModule } from '@angular/material/table';
import { Regulation, RegulationListItem } from '../../../core/models';
import {
  RegulationEditorDialogComponent,
  RegulationEditorDialogData,
  RegulationEditorResult,
} from '../dialogs/regulation-editor-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** The regulation / compliance register (P1-B): filterable list + register/edit/retire/delete. */
@Component({
  selector: 'app-regulation-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    TranslatePipe,
  ],
  templateUrl: './regulation-register.component.html',
  styleUrl: './regulation-register.component.scss',
})
export class RegulationRegisterComponent {
  private readonly service = inject(RegulationsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = ['code', 'name', 'authority', 'category', 'status', 'actions'];

  readonly filters = this.fb.nonNullable.group({
    category: '',
    includeRetired: true,
    search: '',
  });

  readonly state = signal<ViewState>('loading');
  readonly regulations = signal<RegulationListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly canManage = computed(() => this.auth.hasPermission(Permissions.ManageControls));
  readonly isEmpty = computed(() => this.state() === 'ready' && this.regulations().length === 0);

  constructor() {
    this.fetchPage(1);
    this.filters.valueChanges.pipe(debounceTime(250), takeUntilDestroyed()).subscribe(() => this.fetchPage(1));
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    const f = this.filters.getRawValue();
    this.service
      .list({
        category: f.category.trim() || undefined,
        includeRetired: f.includeRetired,
        search: f.search.trim() || undefined,
        page,
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.regulations.set(result.items);
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

  create(): void {
    const data: RegulationEditorDialogData = {};
    this.dialog
      .open(RegulationEditorDialogComponent, { data, width: '520px' })
      .afterClosed()
      .subscribe((result?: RegulationEditorResult) => {
        if (result?.mode !== 'register') {
          return;
        }
        this.service.register(result.body).subscribe({
          next: (r) => {
            this.notify.success(this.i18n.translate('compliance.notify.registered', { code: r.code }));
            this.fetchPage(this.page());
          },
        });
      });
  }

  edit(row: RegulationListItem): void {
    this.service.getById(row.id).subscribe({
      next: (regulation) => this.openEditor(regulation),
    });
  }

  private openEditor(regulation: Regulation): void {
    const data: RegulationEditorDialogData = { regulation };
    this.dialog
      .open(RegulationEditorDialogComponent, { data, width: '520px' })
      .afterClosed()
      .subscribe((result?: RegulationEditorResult) => {
        if (result?.mode !== 'update') {
          return;
        }
        this.service.update(result.id, result.body).subscribe({
          next: (r) => {
            this.notify.success(this.i18n.translate('compliance.notify.updated', { code: r.code }));
            this.fetchPage(this.page());
          },
        });
      });
  }

  toggleActive(row: RegulationListItem): void {
    this.service.getById(row.id).subscribe({
      next: (regulation) =>
        this.service.setStatus(regulation.id, { isActive: !regulation.isActive, version: regulation.version }).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('compliance.notify.statusChanged'));
            this.fetchPage(this.page());
          },
        }),
    });
  }

  remove(row: RegulationListItem): void {
    this.service.getById(row.id).subscribe({
      next: (regulation) =>
        this.service.delete(regulation.id, regulation.version).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('compliance.notify.deleted'));
            this.fetchPage(this.page());
          },
        }),
    });
  }
}
