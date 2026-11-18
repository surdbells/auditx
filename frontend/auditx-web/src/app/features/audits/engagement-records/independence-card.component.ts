import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { IconComponent } from '../../../core/icons/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AuthService } from '../../../core/services/auth.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { IndependenceMember, IndependenceMemberStatus } from '../../../core/models';
import { EngagementRecordsStore } from './engagement-records.store';

const TONE: Record<IndependenceMemberStatus, string> = {
  pending: 'pending',
  declared: 'done',
  conflict_disclosed: 'alert',
  conflict_accepted: 'info',
};

/**
 * The audit team's independence: each member declares they are independent of the area, or discloses a conflict of
 * interest that a manager of the audit (never the person who disclosed it) accepts with safeguards. Where the
 * institution requires it, fieldwork starts only once the whole team is clear. Never shown to the audit's auditee.
 */
@Component({
  selector: 'app-independence-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatCardModule, MatButtonModule, IconComponent, TranslatePipe],
  styleUrl: './engagement-card.scss',
  template: `
    @if (independence(); as ind) {
      <mat-card appearance="outlined" class="eng" data-guide="independence">
        <div class="eng__head">
          <h3 class="eng__title">
            <app-icon name="verified_user" />
            {{ 'engagement.independence.title' | t }}
            @if (ind.complete) {
              <span class="eng__chip" data-tone="done">{{ 'engagement.independence.complete' | t }}</span>
            } @else {
              <span class="eng__chip" data-tone="pending">{{ 'engagement.independence.outstanding' | t: { count: ind.outstanding } }}</span>
            }
          </h3>
          @if (ind.canDeclare) {
            <div class="eng__actions">
              <button
                [matButton]="mine()?.status === 'pending' ? 'filled' : 'tonal'"
                type="button"
                [disabled]="store.busy()"
                (click)="store.declareIndependence()"
              >
                <app-icon name="how_to_reg" />
                {{ (mine()?.status === 'pending' ? 'engagement.independence.action.declare' : 'engagement.independence.action.redeclare') | t }}
              </button>
            </div>
          }
        </div>

        <p class="eng__lead">
          {{ (ind.required ? 'engagement.independence.leadRequired' : 'engagement.independence.leadOptional') | t }}
        </p>

        <ul class="eng__rows">
          @for (m of ind.members; track m.userId) {
            <li class="eng__row">
              <div class="eng__row-head">
                <span class="eng__row-name">{{ nameOf(m.userId) }}</span>
                <span class="eng__muted">{{ 'engagement.role.' + m.teamRole | t }}</span>
                <span class="eng__chip" [attr.data-tone]="tone(m.status)">{{ 'engagement.independence.status.' + m.status | t }}</span>
                @if (m.declaredAt) {
                  <span class="eng__meta">{{ m.declaredAt | date: 'medium' }}</span>
                }
              </div>
              @if (m.conflictDetails) {
                <blockquote class="eng__quote">{{ m.conflictDetails }}</blockquote>
              }
              @if (m.status === 'conflict_accepted') {
                <p class="eng__meta">
                  {{ 'engagement.independence.acceptedBy' | t: { name: nameOf(m.reviewedBy), date: (m.reviewedAt | date: 'medium') } }}
                </p>
                @if (m.reviewNote) {
                  <blockquote class="eng__quote">{{ m.reviewNote }}</blockquote>
                }
              }
              @if (canAccept(m)) {
                <div class="eng__actions">
                  <button matButton="tonal" type="button" [disabled]="store.busy()" (click)="store.acceptConflict(m, nameOf(m.userId))">
                    <app-icon name="gavel" />
                    {{ 'engagement.independence.action.accept' | t }}
                  </button>
                </div>
              }
            </li>
          }
        </ul>
      </mat-card>
    }
  `,
})
export class IndependenceCardComponent {
  readonly store = inject(EngagementRecordsStore);
  private readonly users = inject(UserLookupService);
  private readonly auth = inject(AuthService);

  private readonly me = computed(() => this.auth.session()?.userId ?? null);

  /** Null for the audit's auditee and for a self-assessment: there is nothing for them here. */
  readonly independence = computed(() => this.store.records()?.independence ?? null);

  readonly mine = computed(() => this.independence()?.members.find((m) => m.userId === this.me()) ?? null);

  tone(status: IndependenceMemberStatus): string {
    return TONE[status];
  }

  nameOf(userId: string | null | undefined): string {
    return this.users.displayName(userId);
  }

  /** A manager of the audit decides on someone else's disclosed conflict — never their own. */
  canAccept(m: IndependenceMember): boolean {
    return !!this.independence()?.canDecide && m.status === 'conflict_disclosed' && m.userId !== this.me();
  }
}
