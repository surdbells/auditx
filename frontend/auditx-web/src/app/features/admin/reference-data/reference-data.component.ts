import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ReferenceDataService } from '../../../core/services/reference-data.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  ReferenceDataCategory,
  ReferenceDataItem,
} from '../../../core/models';
import {
  ReferenceDataDialogComponent,
  ReferenceDataDialogData,
  ReferenceDataDialogResult,
} from './dialogs/reference-data-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the reference-data admin (drives the walkthrough + the About panel). */
const REFERENCE_DATA_GUIDE: PageGuide = {
  id: 'reference-data',
  titleKey: 'referenceData.guide.title',
  purposeKey: 'referenceData.guide.purpose',
  descriptionKey: 'referenceData.guide.description',
  actionKeys: [
    'referenceData.guide.action.select',
    'referenceData.guide.action.create',
    'referenceData.guide.action.edit',
    'referenceData.guide.action.archive',
  ],
  sections: [
    { selector: '[data-guide="create"]', titleKey: 'referenceData.guide.section.create.title', bodyKey: 'referenceData.guide.section.create.body' },
    { selector: '.ref__filters-card', titleKey: 'referenceData.guide.section.filters.title', bodyKey: 'referenceData.guide.section.filters.body' },
    { selector: '.ref__table', titleKey: 'referenceData.guide.section.table.title', bodyKey: 'referenceData.guide.section.table.body' },
  ],
  workflowKeys: [
    'referenceData.guide.flow.category',
    'referenceData.guide.flow.add',
    'referenceData.guide.flow.order',
    'referenceData.guide.flow.dropdown',
    'referenceData.guide.flow.use',
  ],
  dependsOnKeys: [
    'referenceData.guide.dep.categories',
    'referenceData.guide.dep.permission',
    'referenceData.guide.dep.setup',
  ],
  usedByKeys: [
    'referenceData.guide.use.audits',
    'referenceData.guide.use.exceptions',
    'referenceData.guide.use.documents',
    'referenceData.guide.use.forms',
  ],
  businessRuleKeys: [
    'referenceData.guide.rule.code',
    'referenceData.guide.rule.archive',
    'referenceData.guide.rule.sort',
    'referenceData.guide.rule.permission',
  ],
  tipKeys: [
    'referenceData.guide.tip.inactive',
    'referenceData.guide.tip.sort',
    'referenceData.guide.tip.archive',
  ],
  permissionKeys: [
    'referenceData.guide.perm.admin',
    'referenceData.guide.perm.all',
  ],
  faq: [
    { questionKey: 'referenceData.guide.faq.edit.q', answerKey: 'referenceData.guide.faq.edit.a' },
    { questionKey: 'referenceData.guide.faq.archive.q', answerKey: 'referenceData.guide.faq.archive.a' },
  ],
};

@Component({
  selector: 'app-reference-data',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatSlideToggleModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './reference-data.component.html',
  styleUrl: './reference-data.component.scss',
})
export class ReferenceDataComponent {
  private readonly service = inject(ReferenceDataService);
  private readonly lookup = inject(ReferenceDataLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);

  readonly guide = REFERENCE_DATA_GUIDE;

  readonly displayedColumns = [
    'code',
    'label',
    'description',
    'sortOrder',
    'active',
    'actions',
  ];

  readonly controls = this.fb.nonNullable.group({
    category: '',
    showInactive: false,
  });

  /** State of the category catalogue itself (loaded once on init). */
  readonly categoriesState = signal<ViewState>('loading');
  readonly categories = signal<ReferenceDataCategory[]>([]);

  /** State of the item list for the selected category. */
  readonly state = signal<ViewState>('loading');
  readonly items = signal<ReferenceDataItem[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageConfiguration),
  );

  readonly selectedCategory = computed(() =>
    this.categories().find((c) => c.code === this.controls.controls.category.value),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.items().length === 0,
  );

  constructor() {
    this.loadCategories();
    // Re-fetch whenever the category or the show-inactive toggle changes.
    this.controls.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetch());
  }

  loadCategories(): void {
    this.categoriesState.set('loading');
    this.service.categories().subscribe({
      next: (categories) => {
        this.categories.set(categories);
        this.categoriesState.set('ready');
        const first = categories[0]?.code ?? '';
        if (first) {
          // Setting the control fires valueChanges → fetch().
          this.controls.controls.category.setValue(first);
        }
      },
      error: () => this.categoriesState.set('error'),
    });
  }

  fetch(): void {
    const { category, showInactive } = this.controls.getRawValue();
    if (!category) {
      return;
    }
    this.state.set('loading');
    this.service.list(category, showInactive).subscribe({
      next: (items) => {
        this.items.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  create(): void {
    const category = this.controls.controls.category.value;
    if (!category) {
      return;
    }
    const data: ReferenceDataDialogData = {
      categoryLabel: this.selectedCategory()?.label ?? category,
    };
    this.dialog
      .open(ReferenceDataDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: ReferenceDataDialogResult) => {
        if (!result || result.mode !== 'create') {
          return;
        }
        this.service.create(category, result.body).subscribe({
          next: (created) => {
            this.notify.success(`"${created.label}" created.`);
            this.afterMutation();
          },
        });
      });
  }

  edit(item: ReferenceDataItem): void {
    const category = this.controls.controls.category.value;
    if (!category) {
      return;
    }
    const data: ReferenceDataDialogData = {
      categoryLabel: this.selectedCategory()?.label ?? category,
      item,
    };
    this.dialog
      .open(ReferenceDataDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: ReferenceDataDialogResult) => {
        if (!result || result.mode !== 'edit') {
          return;
        }
        this.service.update(category, result.id, result.body).subscribe({
          next: (updated) => {
            this.notify.success(`"${updated.label}" updated.`);
            this.afterMutation();
          },
        });
      });
  }

  archive(item: ReferenceDataItem): void {
    const category = this.controls.controls.category.value;
    if (!category) {
      return;
    }
    const data: ConfirmDialogData = {
      title: 'Archive item',
      message: `Archive "${item.label}"? It will no longer appear in dropdowns.`,
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
        this.service.archive(category, item.id).subscribe({
          next: () => {
            this.notify.success(`"${item.label}" archived.`);
            this.afterMutation();
          },
        });
      });
  }

  reactivate(item: ReferenceDataItem): void {
    const category = this.controls.controls.category.value;
    if (!category) {
      return;
    }
    this.service.reactivate(category, item.id).subscribe({
      next: () => {
        this.notify.success(`"${item.label}" reactivated.`);
        this.afterMutation();
      },
    });
  }

  /** Refresh the table and drop the lookup cache so dropdowns pick up the change. */
  private afterMutation(): void {
    this.lookup.invalidate(this.controls.controls.category.value);
    this.fetch();
  }
}
