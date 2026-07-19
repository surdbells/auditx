import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
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
  EngagementJourney,
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
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/**
 * The per-engagement lifecycle journey: a vertical timeline of stages (done / current / pending) with
 * the current step's role-scoped next action(s) highlighted. Actions deep-link into the performing
 * screen or advance the audit inline via the shared transition dialog.
 */
@Component({
  selector: 'app-engagement-journey',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    LoadingComponent,
    ErrorStateComponent,
    TranslatePipe,
  ],
  templateUrl: './engagement-journey.component.html',
  styleUrl: './engagement-journey.component.scss',
})
export class EngagementJourneyComponent {
  /** Route param: the audit (engagement) id. */
  readonly id = input.required<string>();

  private readonly engagements = inject(EngagementService);
  private readonly audits = inject(AuditsService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);
  readonly refLookup = inject(ReferenceDataLookupService);

  readonly state = signal<ViewState>('loading');
  readonly journey = signal<EngagementJourney | null>(null);
  readonly busy = signal(false);

  readonly actions = computed(() => this.journey()?.nextActions ?? []);

  constructor() {
    // Defer until the route-bound input is set.
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.engagements.journey(this.id()).subscribe({
      next: (j) => {
        this.journey.set(j);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  stageLabel(code: string): string {
    const key = `engagement.stage.${code}`;
    const label = this.i18n.translate(key);
    return label === key ? code : label;
  }

  act(action: EngagementNextAction): void {
    const j = this.journey();
    if (!j || this.busy()) {
      return;
    }
    if (action.kind === 'route' && action.route) {
      void this.router.navigateByUrl(action.route);
      return;
    }
    if (action.kind === 'transition' && action.targetState) {
      this.transition(j, action);
    }
  }

  private transition(j: EngagementJourney, action: EngagementNextAction): void {
    const data: TransitionReasonDialogData = {
      title: action.label,
      message: this.i18n.translate('engagement.transition.confirm', { name: j.name }),
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
          .transition(j.auditId, {
            targetState: action.targetState as TransitionTarget,
            reason: result.reason || null,
            version: j.version,
          })
          .subscribe({
            next: () => {
              this.busy.set(false);
              this.notify.success(this.i18n.translate('engagement.transition.done', { name: j.name }));
              this.fetch();
            },
            error: () => this.busy.set(false),
          });
      });
  }
}
