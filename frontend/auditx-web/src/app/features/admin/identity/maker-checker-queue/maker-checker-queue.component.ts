import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatSelectModule } from '@angular/material/select';
import { SelectSearchDirective } from '../../../../shared/directives/select-search.directive';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { MakerCheckerService } from '../../../../core/services/maker-checker.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { UserLookupService } from '../../../../core/services/user-lookup.service';
import { MakerCheckerActionDto } from '../../../../core/models';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import {
  RejectActionDialogComponent,
  RejectActionDialogData,
} from '../dialogs/reject-action-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the maker-checker queue (drives the walkthrough + the About panel). */
const MAKER_CHECKER_GUIDE: PageGuide = {
  id: 'maker-checker-queue',
  titleKey: 'identity.mc.title',
  purposeKey: 'identity.makerChecker.guide.purpose',
  descriptionKey: 'identity.makerChecker.guide.description',
  actionKeys: [
    'identity.makerChecker.guide.action.filter',
    'identity.makerChecker.guide.action.approve',
    'identity.makerChecker.guide.action.reject',
    'identity.makerChecker.guide.action.refresh',
  ],
  sections: [
    { selector: '.mc__filter-card', titleKey: 'identity.makerChecker.guide.section.filter.title', bodyKey: 'identity.makerChecker.guide.section.filter.body' },
    { selector: '.mc__table', titleKey: 'identity.makerChecker.guide.section.table.title', bodyKey: 'identity.makerChecker.guide.section.table.body' },
    { selector: '.mc__actions-col', titleKey: 'identity.makerChecker.guide.section.decision.title', bodyKey: 'identity.makerChecker.guide.section.decision.body' },
  ],
  workflowKeys: [
    'identity.makerChecker.guide.flow.raise',
    'identity.makerChecker.guide.flow.queue',
    'identity.makerChecker.guide.flow.review',
    'identity.makerChecker.guide.flow.decide',
    'identity.makerChecker.guide.flow.apply',
  ],
  dependsOnKeys: [
    'identity.makerChecker.guide.dep.roles',
    'identity.makerChecker.guide.dep.users',
    'identity.makerChecker.guide.dep.policy',
  ],
  usedByKeys: [
    'identity.makerChecker.guide.use.roles',
    'identity.makerChecker.guide.use.users',
    'identity.makerChecker.guide.use.audit',
  ],
  businessRuleKeys: [
    'identity.makerChecker.guide.rule.selfApproval',
    'identity.makerChecker.guide.rule.immediate',
    'identity.makerChecker.guide.rule.reason',
    'identity.makerChecker.guide.rule.scope',
  ],
  tipKeys: [
    'identity.makerChecker.guide.tip.filter',
    'identity.makerChecker.guide.tip.verify',
  ],
  permissionKeys: [
    'identity.makerChecker.guide.perm.authoriser',
    'identity.makerChecker.guide.perm.maker',
  ],
  faq: [
    { questionKey: 'identity.makerChecker.guide.faq.self.q', answerKey: 'identity.makerChecker.guide.faq.self.a' },
    { questionKey: 'identity.makerChecker.guide.faq.approve.q', answerKey: 'identity.makerChecker.guide.faq.approve.a' },
  ],
};

@Component({
  selector: 'app-maker-checker-queue',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    MatChipsModule,
    MatFormFieldModule,
    MatSelectModule,
    SelectSearchDirective,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './maker-checker-queue.component.html',
  styleUrl: './maker-checker-queue.component.scss',
})
export class MakerCheckerQueueComponent {
  private readonly service = inject(MakerCheckerService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  readonly userLookup = inject(UserLookupService);
  private readonly destroyRef = inject(DestroyRef);

  /** The queue refreshes itself so a request raised elsewhere shows up without a reload or a fresh sign-in. */
  private static readonly AutoRefreshMs = 30_000;

  readonly guide = MAKER_CHECKER_GUIDE;

  readonly displayedColumns = [
    'actionType',
    'target',
    'maker',
    'createdAt',
    'actions',
  ];

  /** The action types the server actually raises (the filter used to offer codes it never produces). */
  readonly actionTypes = [
    { value: '', label: 'All action types' },
    { value: 'role_permission_change', label: 'Role & permission change' },
    { value: 'template_publish', label: 'Template publish' },
    { value: 'plan_approval', label: 'Plan approval' },
    { value: 'map_approval', label: 'Action plan approval' },
    { value: 'sanction_application', label: 'Sanction application' },
    { value: 'sanctions_grid_edit', label: 'Sanctions grid change' },
    { value: 'retention_policy_change', label: 'Retention policy change' },
    { value: 'config_activation', label: 'Configuration activation' },
  ];

  readonly actionTypeFilter = new FormControl('', { nonNullable: true });

  readonly state = signal<ViewState>('loading');
  readonly actions = signal<MakerCheckerActionDto[]>([]);
  readonly processingId = signal<string | null>(null);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.actions().length === 0,
  );

  constructor() {
    this.fetch();

    // Keep the queue current: poll while the tab is visible, and refresh as soon as it regains focus.
    const timer = setInterval(() => {
      if (document.visibilityState === 'visible') {
        this.fetch(true);
      }
    }, MakerCheckerQueueComponent.AutoRefreshMs);
    const onFocus = () => this.fetch(true);
    window.addEventListener('focus', onFocus);
    this.destroyRef.onDestroy(() => {
      clearInterval(timer);
      window.removeEventListener('focus', onFocus);
    });
  }

  /** Loads the queue. A quiet refresh keeps the current rows on screen and never flips to the error state. */
  fetch(quiet = false): void {
    if (quiet && (this.state() !== 'ready' || this.processingId())) {
      return;
    }
    if (!quiet) {
      this.state.set('loading');
    }
    const filter = this.actionTypeFilter.value || undefined;
    this.service.pending(filter).subscribe({
      next: (actions) => {
        this.actions.set(actions);
        this.state.set('ready');
      },
      error: () => {
        if (!quiet) {
          this.state.set('error');
        }
      },
    });
  }

  /** A readable name for an action type code. */
  typeLabel(type: string): string {
    return this.actionTypes.find((t) => t.value === type)?.label ?? type;
  }

  onFilterChange(): void {
    this.fetch();
  }

  approve(action: MakerCheckerActionDto): void {
    const data: ConfirmDialogData = {
      title: 'Approve change',
      message: `Approve "${this.actionLabel(action)}" raised by ${action.makerName || this.userLookup.displayName(action.makerUserId)}? This will apply the change immediately.`,
      confirmLabel: 'Approve',
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.processingId.set(action.id);
        this.service.approve(action.id).subscribe({
          next: () => {
            this.processingId.set(null);
            this.notify.success('Change approved.');
            this.removeAction(action.id);
          },
          error: (err: HttpErrorResponse) => {
            this.processingId.set(null);
            if (err.status === 403) {
              // Self-approval guard. The interceptor already toasts the server
              // message; reinforce with a clear, specific hint.
              this.notify.warning(
                'You cannot approve a change you raised. A different authoriser must approve it.',
              );
            }
          },
        });
      });
  }

  reject(action: MakerCheckerActionDto): void {
    const data: RejectActionDialogData = {
      actionLabel: this.actionLabel(action),
    };
    this.dialog
      .open(RejectActionDialogComponent, { data })
      .afterClosed()
      .subscribe((reason: string | undefined) => {
        if (!reason) {
          return;
        }
        this.processingId.set(action.id);
        this.service.reject(action.id, { reason }).subscribe({
          next: () => {
            this.processingId.set(null);
            this.notify.success('Change rejected.');
            this.removeAction(action.id);
          },
          error: (err: HttpErrorResponse) => {
            this.processingId.set(null);
            if (err.status === 403) {
              this.notify.warning(
                'You cannot reject a change you raised.',
              );
            }
          },
        });
      });
  }

  actionLabel(action: MakerCheckerActionDto): string {
    return `${this.typeLabel(action.actionType)} on ${action.targetObjectType}`;
  }

  /**
   * A human label for the polymorphic target: a resolved name when the target is a user (the common maker-checker
   * case — role grants, status changes), else empty so the bare GUID is hidden (the target type line above already
   * identifies the object; a raw id conveys nothing to the approver).
   */
  targetLabel(action: MakerCheckerActionDto): string {
    if (!action.targetObjectId) {
      return '';
    }
    return action.targetObjectType?.toLowerCase() === 'user'
      ? this.userLookup.displayName(action.targetObjectId)
      : '';
  }

  private removeAction(id: string): void {
    this.actions.update((list) => list.filter((a) => a.id !== id));
  }
}
