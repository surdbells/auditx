import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { debounceTime } from 'rxjs';

import { UniverseService } from '../../../core/services/universe.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { EntityListItem } from '../../../core/models';
import {
  EntityEditorDialogComponent,
  EntityEditorDialogData,
} from '../dialogs/entity-editor-dialog.component';
import { BulkImportDialogComponent } from '../dialogs/bulk-import-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Heat-map band keyed off a composite residual score (typical scale 1–5). */
export type HeatBand = 'none' | 'low' | 'moderate' | 'high' | 'critical';

/** Contextual page guide for the audit-universe entities list (walkthrough + About panel). */
const UNIVERSE_GUIDE: PageGuide = {
  id: 'universe-entities-list',
  titleKey: 'universe.list.title',
  purposeKey: 'universe.guide.purpose',
  descriptionKey: 'universe.guide.description',
  actionKeys: [
    'universe.guide.action.create',
    'universe.guide.action.filter',
    'universe.guide.action.score',
    'universe.guide.action.import',
  ],
  sections: [
    { selector: '[data-guide="create"]', titleKey: 'universe.guide.section.create.title', bodyKey: 'universe.guide.section.create.body' },
    { selector: '.entities__filters-card', titleKey: 'universe.guide.section.filters.title', bodyKey: 'universe.guide.section.filters.body' },
    { selector: '.entities__table', titleKey: 'universe.guide.section.table.title', bodyKey: 'universe.guide.section.table.body' },
  ],
  workflowKeys: ['universe.guide.flow.define', 'universe.guide.flow.dimensions', 'universe.guide.flow.score', 'universe.guide.flow.rank', 'universe.guide.flow.plan'],
  dependsOnKeys: ['universe.guide.dep.dimensions', 'universe.guide.dep.orgUnits', 'universe.guide.dep.users'],
  usedByKeys: ['universe.guide.use.plan', 'universe.guide.use.audits', 'universe.guide.use.analytics'],
  businessRuleKeys: ['universe.guide.rule.residual', 'universe.guide.rule.heat', 'universe.guide.rule.archive', 'universe.guide.rule.score'],
  tipKeys: ['universe.guide.tip.import', 'universe.guide.tip.heat', 'universe.guide.tip.archived'],
  permissionKeys: ['universe.guide.perm.manage', 'universe.guide.perm.score'],
  faq: [
    { questionKey: 'universe.guide.faq.residual.q', answerKey: 'universe.guide.faq.residual.a' },
    { questionKey: 'universe.guide.faq.delete.q', answerKey: 'universe.guide.faq.delete.a' },
  ],
};

@Component({
  selector: 'app-entities-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './entities-list.component.html',
  styleUrl: './entities-list.component.scss',
})
export class EntitiesListComponent {
  private readonly universe = inject(UniverseService);
  /** Resolves owner user ids to display names in the table. */
  readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'name',
    'entityType',
    'owner',
    'residual',
    'lastAudited',
    'actions',
  ];

  readonly entityTypes = signal<string[]>([]);

  readonly filters = this.fb.nonNullable.group({
    entityType: '',
    owner: '',
    search: '',
    archived: false,
  });

  readonly state = signal<ViewState>('loading');
  readonly entities = signal<EntityListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageUniverse),
  );
  readonly canScore = computed(() =>
    this.auth.hasPermission(Permissions.ScoreRisk),
  );
  readonly canOpenEditor = computed(
    () => this.canManage() || this.canScore(),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.entities().length === 0,
  );

  readonly guide = UNIVERSE_GUIDE;

  constructor() {
    this.loadEntityTypes();
    this.fetchPage(1);

    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
  }

  private loadEntityTypes(): void {
    this.universe.entityTypes().subscribe({
      next: (types) => this.entityTypes.set(types),
      error: () => {
        // Non-fatal: the filter just stays empty.
      },
    });
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    const { entityType, owner, search, archived } = this.filters.getRawValue();
    this.universe
      .list({
        entityType,
        owner,
        search,
        archived: archived ? true : undefined,
        page,
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.entities.set(result.items);
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
    this.filters.reset({
      entityType: '',
      owner: '',
      search: '',
      archived: false,
    });
  }

  /** Maps a composite residual score onto a heat-map band. */
  heatBand(score: number | null): HeatBand {
    if (score === null || score === undefined) {
      return 'none';
    }
    if (score >= 4) {
      return 'critical';
    }
    if (score >= 3) {
      return 'high';
    }
    if (score >= 2) {
      return 'moderate';
    }
    return 'low';
  }

  create(): void {
    const data: EntityEditorDialogData = {
      entityTypes: this.entityTypes(),
      parentCandidates: this.entities(),
    };
    this.dialog
      .open(EntityEditorDialogComponent, { data, width: '640px' })
      .afterClosed()
      .subscribe((changed) => {
        if (changed) {
          this.fetchPage(this.page());
        }
      });
  }

  open(entity: EntityListItem): void {
    if (!this.canOpenEditor()) {
      return;
    }
    const data: EntityEditorDialogData = {
      entity,
      entityTypes: this.entityTypes(),
      parentCandidates: this.entities().filter((e) => e.id !== entity.id),
    };
    this.dialog
      .open(EntityEditorDialogComponent, { data, width: '640px' })
      .afterClosed()
      .subscribe((changed) => {
        if (changed) {
          this.fetchPage(this.page());
        }
      });
  }

  bulkImport(): void {
    this.dialog
      .open(BulkImportDialogComponent, { width: '640px' })
      .afterClosed()
      .subscribe((created?: number) => {
        if (created !== undefined) {
          this.notify.success(
            this.i18n.translate('universe.notify.imported', { count: created }),
          );
          this.fetchPage(this.page());
        }
      });
  }

  archive(entity: EntityListItem, event: Event): void {
    event.stopPropagation();
    const data: ConfirmDialogData = {
      title: this.i18n.translate('universe.archive.title'),
      message: this.i18n.translate('universe.archive.message', {
        name: entity.name,
      }),
      confirmLabel: this.i18n.translate('universe.actions.archive'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.universe.archive(entity.id).subscribe({
          next: () => {
            this.notify.success(
              this.i18n.translate('universe.notify.archived', {
                name: entity.name,
              }),
            );
            this.fetchPage(this.page());
          },
        });
      });
  }
}
