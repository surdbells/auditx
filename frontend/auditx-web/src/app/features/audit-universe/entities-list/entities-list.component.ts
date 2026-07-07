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

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 20;

/** Heat-map band keyed off a composite residual score (typical scale 1–5). */
export type HeatBand = 'none' | 'low' | 'moderate' | 'high' | 'critical';

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
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

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

  constructor() {
    this.loadEntityTypes();
    this.fetchFirstPage();

    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchFirstPage());
  }

  private loadEntityTypes(): void {
    this.universe.entityTypes().subscribe({
      next: (types) => this.entityTypes.set(types),
      error: () => {
        // Non-fatal: the filter just stays empty.
      },
    });
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.entities.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.entities.set(items);
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
      this.entities.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: EntityListItem[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    const { entityType, owner, search, archived } = this.filters.getRawValue();
    this.universe
      .list({
        entityType,
        owner,
        search,
        archived: archived ? true : undefined,
        cursor,
        limit: PAGE_SIZE,
      })
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
          this.fetchFirstPage();
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
          this.fetchFirstPage();
        }
      });
  }

  bulkImport(): void {
    this.dialog
      .open(BulkImportDialogComponent, { width: '640px' })
      .afterClosed()
      .subscribe((created?: number) => {
        if (created !== undefined) {
          this.notify.success(`${created} entit(y/ies) imported.`);
          this.fetchFirstPage();
        }
      });
  }

  archive(entity: EntityListItem, event: Event): void {
    event.stopPropagation();
    const data: ConfirmDialogData = {
      title: 'Archive entity',
      message: `Archive "${entity.name}"? It will be hidden from the active universe.`,
      confirmLabel: 'Archive',
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
            this.notify.success(`"${entity.name}" archived.`);
            this.fetchFirstPage();
          },
        });
      });
  }
}
