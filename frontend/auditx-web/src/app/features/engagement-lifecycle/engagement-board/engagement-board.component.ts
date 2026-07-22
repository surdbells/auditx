import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';

import { EngagementService } from '../../../core/services/engagement.service';
import { AuditsService } from '../../../core/services/audits.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import {
  EngagementBoardItem,
  EngagementNextAction,
  TransitionTarget,
} from '../../../core/models';
import {
  TransitionReasonDialogComponent,
  TransitionReasonDialogData,
  TransitionReasonResult,
} from '../../audits/dialogs/transition-reason-dialog.component';
import { IconComponent } from '../../../core/icons/icon.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the engagement lifecycle board (drives the walkthrough + the About panel). */
const ENGAGEMENT_BOARD_GUIDE: PageGuide = {
  id: 'engagement-board',
  titleKey: 'engagement.board.title',
  purposeKey: 'engagement.board.guide.purpose',
  descriptionKey: 'engagement.board.guide.description',
  actionKeys: [
    'engagement.board.guide.action.filter',
    'engagement.board.guide.action.act',
    'engagement.board.guide.action.journey',
  ],
  sections: [
    { selector: '.eb__toolbar', titleKey: 'engagement.board.guide.section.toolbar.title', bodyKey: 'engagement.board.guide.section.toolbar.body' },
    { selector: '.eb__grid', titleKey: 'engagement.board.guide.section.grid.title', bodyKey: 'engagement.board.guide.section.grid.body' },
  ],
  workflowKeys: [
    'engagement.board.guide.flow.setup',
    'engagement.board.guide.flow.planned',
    'engagement.board.guide.flow.fieldwork',
    'engagement.board.guide.flow.review',
    'engagement.board.guide.flow.remediation',
    'engagement.board.guide.flow.reporting',
    'engagement.board.guide.flow.closed',
  ],
  dependsOnKeys: ['engagement.board.guide.dep.audits', 'engagement.board.guide.dep.teams'],
  usedByKeys: ['engagement.board.guide.use.journey'],
  businessRuleKeys: ['engagement.board.guide.rule.nextAction', 'engagement.board.guide.rule.roleGated'],
  tipKeys: ['engagement.board.guide.tip.waiting', 'engagement.board.guide.tip.progress'],
  permissionKeys: ['engagement.board.guide.perm.view'],
  faq: [
    { questionKey: 'engagement.board.guide.faq.action.q', answerKey: 'engagement.board.guide.faq.action.a' },
  ],
};

/**
 * The Engagement Lifecycle portfolio board: every engagement the user is part of, each with its
 * lifecycle stage and the user's role-scoped next best action(s). Route actions deep-link into the
 * screen that performs the step; transition actions advance the audit inline via the shared dialog.
 */
@Component({
  selector: 'app-engagement-board',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './engagement-board.component.html',
  styleUrl: './engagement-board.component.scss',
})
export class EngagementBoardComponent {
  private readonly engagements = inject(EngagementService);
  private readonly audits = inject(AuditsService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);
  readonly refLookup = inject(ReferenceDataLookupService);

  readonly state = signal<ViewState>('loading');
  readonly items = signal<EngagementBoardItem[]>([]);
  /** When on, only engagements with an action waiting on the user are shown. */
  readonly waitingOnly = signal(false);
  readonly busy = signal(false);

  readonly visible = computed(() =>
    this.waitingOnly() ? this.items().filter((i) => i.waitingOnMe) : this.items(),
  );
  readonly waitingCount = computed(() => this.items().filter((i) => i.waitingOnMe).length);
  readonly isEmpty = computed(() => this.state() === 'ready' && this.visible().length === 0);

  readonly guide = ENGAGEMENT_BOARD_GUIDE;

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.engagements.board().subscribe({
      next: (items) => {
        this.items.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  toggleWaiting(): void {
    this.waitingOnly.update((v) => !v);
  }

  /** Localised stage label with the backend code as a fallback. */
  stageLabel(code: string): string {
    const key = `engagement.stage.${code}`;
    const label = this.i18n.translate(key);
    return label === key ? code : label;
  }

  act(item: EngagementBoardItem, action: EngagementNextAction): void {
    if (this.busy()) {
      return;
    }
    if (action.kind === 'route' && action.route) {
      void this.router.navigateByUrl(action.route);
      return;
    }
    if (action.kind === 'transition' && action.targetState) {
      this.transition(item, action);
    }
  }

  private transition(item: EngagementBoardItem, action: EngagementNextAction): void {
    const data: TransitionReasonDialogData = {
      title: action.label,
      message: this.i18n.translate('engagement.transition.confirm', { name: item.name }),
      reasonRequired: false,
      confirmLabel: action.label,
    };
    this.dialog
      .open(TransitionReasonDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: TransitionReasonResult) => {
        if (!result) {
          return;
        }
        this.busy.set(true);
        this.audits
          .transition(item.auditId, {
            targetState: action.targetState as TransitionTarget,
            reason: result.reason || null,
            version: item.version,
          })
          .subscribe({
            next: () => {
              this.busy.set(false);
              this.notify.success(this.i18n.translate('engagement.transition.done', { name: item.name }));
              this.fetch();
            },
            error: () => this.busy.set(false),
          });
      });
  }
}
