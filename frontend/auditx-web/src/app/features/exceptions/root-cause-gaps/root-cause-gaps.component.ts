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
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { RootCauseGapsService } from '../../../core/services/root-cause-gaps.service';
import { UsersService } from '../../../core/services/users.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  RootCauseGapListItem,
  RootCauseGapStatus,
  UserDto,
} from '../../../core/models';
import {
  RootCauseGapDialogComponent,
  RootCauseGapDialogData,
  RootCauseGapDialogResult,
} from './dialogs/root-cause-gap-dialog.component';
import {
  RootCauseGapDetailDialogComponent,
  RootCauseGapDetailDialogData,
} from './dialogs/root-cause-gap-detail-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';
type StatusFilter = RootCauseGapStatus | 'all';

const ROOT_CAUSE_GAPS_GUIDE: PageGuide = {
  id: 'root-cause-gaps',
  titleKey: 'rootCauseGaps.title',
  purposeKey: 'rootCauseGaps.guide.purpose',
  descriptionKey: 'rootCauseGaps.guide.description',
  actionKeys: [
    'rootCauseGaps.guide.action.create',
    'rootCauseGaps.guide.action.link',
    'rootCauseGaps.guide.action.close',
  ],
  sections: [
    {
      selector: '.rcg__table-card',
      titleKey: 'rootCauseGaps.guide.section.table.title',
      bodyKey: 'rootCauseGaps.guide.section.table.body',
    },
  ],
  usedByKeys: ['rootCauseGaps.guide.use.findings'],
  businessRuleKeys: ['rootCauseGaps.guide.rule.rationale', 'rootCauseGaps.guide.rule.lock'],
  tipKeys: ['rootCauseGaps.guide.tip.recurring'],
  permissionKeys: ['rootCauseGaps.guide.perm.manage'],
};

@Component({
  selector: 'app-root-cause-gaps',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatMenuModule,
    IconComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './root-cause-gaps.component.html',
  styleUrl: './root-cause-gaps.component.scss',
})
export class RootCauseGapsComponent {
  private readonly service = inject(RootCauseGapsService);
  private readonly users = inject(UsersService);
  private readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = ['title', 'owner', 'linked', 'target', 'status', 'actions'];

  readonly filterOptions: { value: StatusFilter; label: string }[] = [
    { value: 'open', label: this.i18n.translate('rootCauseGaps.status.open') },
    { value: 'closed', label: this.i18n.translate('rootCauseGaps.status.closed') },
    { value: 'all', label: this.i18n.translate('rootCauseGaps.filter.all') },
  ];

  readonly filters = this.fb.nonNullable.group({ status: 'open' as StatusFilter });

  readonly state = signal<ViewState>('loading');
  readonly gaps = signal<RootCauseGapListItem[]>([]);
  private usersCache: UserDto[] = [];

  readonly canManage = computed(() => this.auth.hasPermission(Permissions.ManageException));

  readonly isEmpty = computed(() => this.state() === 'ready' && this.gaps().length === 0);

  readonly guide = ROOT_CAUSE_GAPS_GUIDE;

  constructor() {
    this.fetch();
    this.filters.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.list({ status: this.filters.getRawValue().status, page: 1, pageSize: 0 }).subscribe({
      next: (page) => {
        this.gaps.set(page.items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  ownerName(id: string): string {
    return this.userLookup.displayName(id);
  }

  private withUsers(run: (users: UserDto[]) => void): void {
    if (this.usersCache.length) {
      run(this.usersCache);
      return;
    }
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        run(page.items);
      },
      error: () => run([]),
    });
  }

  create(): void {
    this.withUsers((users) => {
      const data: RootCauseGapDialogData = { users };
      this.dialog
        .open(RootCauseGapDialogComponent, { data, width: '560px' })
        .afterClosed()
        .subscribe((result?: RootCauseGapDialogResult) => {
          if (!result || result.mode !== 'create') {
            return;
          }
          this.service.create(result.body).subscribe({
            next: () => {
              this.notify.success(this.i18n.translate('rootCauseGaps.notify.created'));
              this.fetch();
            },
          });
        });
    });
  }

  edit(row: RootCauseGapListItem): void {
    this.service.getById(row.id).subscribe((gap) => {
      this.withUsers((users) => {
        const data: RootCauseGapDialogData = { gap, users };
        this.dialog
          .open(RootCauseGapDialogComponent, { data, width: '560px' })
          .afterClosed()
          .subscribe((result?: RootCauseGapDialogResult) => {
            if (!result || result.mode !== 'edit') {
              return;
            }
            this.service.update(result.id, result.body).subscribe({
              next: () => {
                this.notify.success(this.i18n.translate('rootCauseGaps.notify.updated'));
                this.fetch();
              },
            });
          });
      });
    });
  }

  open(row: RootCauseGapListItem): void {
    const data: RootCauseGapDetailDialogData = { gapId: row.id };
    this.dialog
      .open(RootCauseGapDetailDialogComponent, { data, width: '600px' })
      .afterClosed()
      .subscribe((changed?: boolean) => {
        if (changed) {
          this.fetch();
        }
      });
  }
}
