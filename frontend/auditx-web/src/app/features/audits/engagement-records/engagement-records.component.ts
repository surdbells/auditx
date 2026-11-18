import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { Audit } from '../../../core/models';
import { EngagementRecordsStore } from './engagement-records.store';
import { EngagementLetterCardComponent } from './engagement-letter-card.component';
import { IndependenceCardComponent } from './independence-card.component';
import { EngagementMeetingsCardComponent } from './engagement-meetings-card.component';

/**
 * The audit's engagement records on the audit page: the engagement letter, the team's independence and the kickoff and
 * exit meetings. Uses the page's {@link EngagementRecordsStore} (the page reads the independence state for its Start
 * button), loads it whenever the audit (re)loads, and asks the page to reload the audit when a change moved its version.
 */
@Component({
  selector: 'app-engagement-records',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EngagementLetterCardComponent, IndependenceCardComponent, EngagementMeetingsCardComponent],
  template: `
    @if (store.records(); as r) {
      @if (!r.isSelfAssessment) {
        <app-engagement-letter-card [audit]="audit()" />
        <app-independence-card />
      }
    }
    <app-engagement-meetings-card
      [audit]="audit()"
      [canScheduleKickoff]="canScheduleKickoff()"
      (scheduleKickoff)="scheduleKickoff.emit()"
    />
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class EngagementRecordsComponent {
  readonly store = inject(EngagementRecordsStore);

  readonly audit = input.required<Audit>();
  readonly canScheduleKickoff = input(false);

  readonly reloadRequested = output<void>();
  readonly scheduleKickoff = output<void>();

  constructor() {
    effect(() => {
      this.store.setAudit(this.audit());
      this.store.load();
    });
    this.store.auditChanged.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(() => this.reloadRequested.emit());
  }
}
