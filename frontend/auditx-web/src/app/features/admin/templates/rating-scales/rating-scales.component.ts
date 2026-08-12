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
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import {
  RatingScaleFilter,
  RatingScalesService,
} from '../../../../core/services/rating-scales.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { RatingScale, RatingScalePoint } from '../../../../core/models';
import {
  RatingScaleDialogComponent,
  RatingScaleDialogData,
  RatingScaleDialogResult,
} from '../dialogs/rating-scale-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const RATING_SCALES_GUIDE: PageGuide = {
  id: 'rating-scales',
  titleKey: 'templatesAdmin.ratingScales.title',
  purposeKey: 'templatesAdmin.ratingScales.guide.purpose',
  descriptionKey: 'templatesAdmin.ratingScales.guide.description',
  actionKeys: [
    'templatesAdmin.ratingScales.guide.action.create',
    'templatesAdmin.ratingScales.guide.action.edit',
  ],
  sections: [
    {
      selector: '.rating-scales__table-card',
      titleKey: 'templatesAdmin.ratingScales.guide.section.table.title',
      bodyKey: 'templatesAdmin.ratingScales.guide.section.table.body',
    },
  ],
  usedByKeys: ['templatesAdmin.ratingScales.guide.use.items'],
  businessRuleKeys: [
    'templatesAdmin.ratingScales.guide.rule.scoring',
    'templatesAdmin.ratingScales.guide.rule.deactivate',
  ],
  tipKeys: ['templatesAdmin.ratingScales.guide.tip.reuse'],
  permissionKeys: ['templatesAdmin.ratingScales.guide.perm.manage'],
};

@Component({
  selector: 'app-rating-scales',
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
  templateUrl: './rating-scales.component.html',
  styleUrl: './rating-scales.component.scss',
})
export class RatingScalesComponent {
  private readonly service = inject(RatingScalesService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = ['name', 'points', 'active', 'actions'];

  readonly filterOptions: { value: RatingScaleFilter; label: string }[] = [
    { value: 'true', label: this.i18n.translate('universe.filter.active') },
    { value: 'false', label: this.i18n.translate('universe.filter.inactive') },
    { value: 'all', label: this.i18n.translate('universe.filter.all') },
  ];

  readonly filters = this.fb.nonNullable.group({
    active: 'true' as RatingScaleFilter,
  });

  readonly state = signal<ViewState>('loading');
  readonly scales = signal<RatingScale[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageTemplates),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.scales().length === 0,
  );

  readonly guide = RATING_SCALES_GUIDE;

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
        this.scales.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  points(scale: RatingScale): RatingScalePoint[] {
    try {
      const parsed = JSON.parse(scale.pointsJson);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  create(): void {
    const data: RatingScaleDialogData = {};
    this.dialog
      .open(RatingScaleDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: RatingScaleDialogResult) => {
        if (!result || result.mode !== 'create') {
          return;
        }
        this.service.create(result.body).subscribe({
          next: (created) => {
            this.notify.success(
              this.i18n.translate('templatesAdmin.ratingScales.notify.created', {
                name: created.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }

  edit(scale: RatingScale): void {
    const data: RatingScaleDialogData = { scale };
    this.dialog
      .open(RatingScaleDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: RatingScaleDialogResult) => {
        if (!result || result.mode !== 'edit') {
          return;
        }
        this.service.update(result.id, result.body).subscribe({
          next: (updated) => {
            this.notify.success(
              this.i18n.translate('templatesAdmin.ratingScales.notify.updated', {
                name: updated.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }
}
