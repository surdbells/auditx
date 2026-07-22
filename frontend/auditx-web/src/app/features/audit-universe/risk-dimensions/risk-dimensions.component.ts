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
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { RiskDimensionsService } from '../../../core/services/risk-dimensions.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { RiskDimension, RiskDimensionFilter } from '../../../core/models';
import {
  RiskDimensionDialogComponent,
  RiskDimensionDialogData,
  RiskDimensionDialogResult,
} from '../dialogs/risk-dimension-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for risk-dimension configuration (drives the walkthrough + the About panel). */
const RISK_DIMENSIONS_GUIDE: PageGuide = {
  id: 'risk-dimensions',
  titleKey: 'universe.dims.title',
  purposeKey: 'universe.dims.guide.purpose',
  descriptionKey: 'universe.dims.guide.description',
  actionKeys: ['universe.dims.guide.action.create', 'universe.dims.guide.action.edit', 'universe.dims.guide.action.filter'],
  sections: [
    { selector: '.dims__table-card', titleKey: 'universe.dims.guide.section.table.title', bodyKey: 'universe.dims.guide.section.table.body' },
  ],
  usedByKeys: ['universe.dims.guide.use.universe', 'universe.dims.guide.use.heatmap'],
  businessRuleKeys: ['universe.dims.guide.rule.weight', 'universe.dims.guide.rule.scale', 'universe.dims.guide.rule.deactivate'],
  tipKeys: ['universe.dims.guide.tip.weights'],
  permissionKeys: ['universe.dims.guide.perm.manage'],
};

@Component({
  selector: 'app-risk-dimensions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    IconComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './risk-dimensions.component.html',
  styleUrl: './risk-dimensions.component.scss',
})
export class RiskDimensionsComponent {
  private readonly service = inject(RiskDimensionsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'name',
    'weight',
    'scale',
    'active',
    'actions',
  ];

  readonly filterOptions: { value: RiskDimensionFilter; label: string }[] = [
    { value: 'true', label: this.i18n.translate('universe.filter.active') },
    { value: 'false', label: this.i18n.translate('universe.filter.inactive') },
    { value: 'all', label: this.i18n.translate('universe.filter.all') },
  ];

  readonly filters = this.fb.nonNullable.group({
    active: 'true' as RiskDimensionFilter,
  });

  readonly state = signal<ViewState>('loading');
  readonly dimensions = signal<RiskDimension[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageConfiguration),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.dimensions().length === 0,
  );

  readonly guide = RISK_DIMENSIONS_GUIDE;

  constructor() {
    this.fetch();
    this.filters.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.list(this.filters.getRawValue().active).subscribe({
      next: (items) => {
        this.dimensions.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  create(): void {
    const data: RiskDimensionDialogData = {};
    this.dialog
      .open(RiskDimensionDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: RiskDimensionDialogResult) => {
        if (!result || result.mode !== 'create') {
          return;
        }
        this.service.create(result.body).subscribe({
          next: (created) => {
            this.notify.success(
              this.i18n.translate('universe.notify.dimCreated', {
                name: created.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }

  edit(dimension: RiskDimension): void {
    const data: RiskDimensionDialogData = { dimension };
    this.dialog
      .open(RiskDimensionDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: RiskDimensionDialogResult) => {
        if (!result || result.mode !== 'edit') {
          return;
        }
        this.service.update(result.id, result.body).subscribe({
          next: (updated) => {
            this.notify.success(
              this.i18n.translate('universe.notify.dimUpdated', {
                name: updated.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }
}
