import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { IconComponent } from '../../../../core/icons/icon.component';
import { RootCauseGapsService } from '../../../../core/services/root-cause-gaps.service';
import { ExceptionsService } from '../../../../core/services/exceptions.service';
import { UsersService } from '../../../../core/services/users.service';
import { UserLookupService } from '../../../../core/services/user-lookup.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { ExceptionListItem, RootCauseGap, RootCauseGapRemediation, UserDto } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

export interface RootCauseGapDetailDialogData {
  gapId: string;
}

/** Manage one root-cause gap: view its linked findings, link/unlink findings, and close/reopen it. */
@Component({
  selector: 'app-root-cause-gap-detail-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
  ],
  template: `
    @if (gap(); as g) {
      <h2 mat-dialog-title>
        {{ g.title }}
        <span class="status-badge" [attr.data-status]="g.status === 'open' ? 'in_progress' : 'system'">
          {{ 'rootCauseGaps.status.' + g.status | t }}
        </span>
      </h2>
      <mat-dialog-content>
        @if (g.description) {
          <p class="desc">{{ g.description }}</p>
        }
        @if (g.status === 'closed' && g.closureRationale) {
          <p class="closure"><strong>{{ 'rootCauseGaps.closureRationale' | t }}:</strong> {{ g.closureRationale }}</p>
        }

        <h3 class="section-title">{{ 'rootCauseGaps.linkedFindings' | t }}</h3>
        @if (canManage() && g.status === 'open') {
          <form [formGroup]="linkForm" class="add-row">
            <mat-form-field appearance="outline" class="grow">
              <mat-label>{{ 'rootCauseGaps.linkLabel' | t }}</mat-label>
              <mat-select formControlName="exceptionId">
                @for (e of availableFindings(); track e.id) {
                  <mat-option [value]="e.id">{{ e.title }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <button matButton="filled" type="button" [disabled]="!linkForm.controls.exceptionId.value" (click)="link()">
              <app-icon name="add" />
              {{ 'rootCauseGaps.link' | t }}
            </button>
          </form>
        }

        @if (!g.linkedExceptions.length) {
          <p class="muted">{{ 'rootCauseGaps.noFindings' | t }}</p>
        } @else {
          <ul class="links">
            @for (e of g.linkedExceptions; track e.linkId) {
              <li class="links__item">
                <div class="links__body">
                  <span class="links__title">{{ e.title }}</span>
                  <span class="muted links__meta">{{ 'exceptions.severity.' + e.severity | t }} · {{ statusLabel(e.status) }}</span>
                </div>
                @if (canManage() && g.status === 'open') {
                  <button matIconButton type="button" (click)="unlink(e.exceptionId)" [attr.aria-label]="'rootCauseGaps.unlink' | t">
                    <app-icon name="link_off" />
                  </button>
                }
              </li>
            }
          </ul>
        }

        <h3 class="section-title">{{ 'rootCauseGaps.remediation.title' | t }}</h3>
        @if (canManage() && g.status === 'open') {
          <form [formGroup]="remediationForm" class="rem-add">
            <mat-form-field appearance="outline" class="full">
              <mat-label>{{ 'rootCauseGaps.remediation.description' | t }}</mat-label>
              <input matInput formControlName="description" autocomplete="off" />
            </mat-form-field>
            <div class="rem-add__row">
              <mat-form-field appearance="outline" class="grow">
                <mat-label>{{ 'rootCauseGaps.remediation.owner' | t }}</mat-label>
                <mat-select formControlName="ownerUserId">
                  @for (u of users(); track u.id) {
                    <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>{{ 'rootCauseGaps.remediation.dueDate' | t }}</mat-label>
                <input matInput type="date" formControlName="dueDate" />
              </mat-form-field>
              <button matButton="filled" type="button"
                [disabled]="!remediationForm.controls.description.value.trim() || !remediationForm.controls.ownerUserId.value"
                (click)="addRemediation()">
                <app-icon name="add" />
                {{ 'rootCauseGaps.remediation.add' | t }}
              </button>
            </div>
          </form>
        }

        @if (!g.remediations.length) {
          <p class="muted">{{ 'rootCauseGaps.remediation.empty' | t }}</p>
        } @else {
          <ul class="links">
            @for (r of g.remediations; track r.id) {
              <li class="links__item" [class.rem--done]="r.status === 'completed'">
                <div class="links__body">
                  <span class="links__title">{{ r.description }}</span>
                  <span class="muted links__meta">
                    {{ nameOf(r.ownerUserId) }}
                    @if (r.dueDate) { · {{ 'rootCauseGaps.remediation.due' | t }} {{ r.dueDate | date: 'mediumDate' }} }
                    · <span class="status-badge" [attr.data-status]="r.status === 'completed' ? 'system' : 'in_progress'">{{ 'rootCauseGaps.remediation.status.' + r.status | t }}</span>
                  </span>
                </div>
                @if (canManage() && g.status === 'open') {
                  <div class="rem-actions">
                    @if (r.status === 'open') {
                      <button matIconButton type="button" (click)="completeRemediation(r)" [attr.aria-label]="'rootCauseGaps.remediation.complete' | t">
                        <app-icon name="check_circle" />
                      </button>
                    } @else {
                      <button matIconButton type="button" (click)="reopenRemediation(r)" [attr.aria-label]="'rootCauseGaps.remediation.reopen' | t">
                        <app-icon name="refresh" />
                      </button>
                    }
                    <button matIconButton type="button" (click)="removeRemediation(r)" [attr.aria-label]="'rootCauseGaps.remediation.remove' | t">
                      <app-icon name="delete" />
                    </button>
                  </div>
                }
              </li>
            }
          </ul>
        }

        @if (canManage() && g.status === 'open') {
          <h3 class="section-title">{{ 'rootCauseGaps.close.title' | t }}</h3>
          <form [formGroup]="closeForm" class="close-row">
            <mat-form-field appearance="outline" class="full">
              <mat-label>{{ 'rootCauseGaps.close.rationale' | t }}</mat-label>
              <textarea matInput formControlName="rationale" rows="2"></textarea>
              <mat-hint>{{ 'rootCauseGaps.close.rationaleHint' | t }}</mat-hint>
            </mat-form-field>
            <button matButton="tonal" type="button" [disabled]="closeForm.controls.rationale.value.trim().length < 10" (click)="close()">
              <app-icon name="check_circle" />
              {{ 'rootCauseGaps.close.action' | t }}
            </button>
          </form>
        }

        <p class="muted meta">
          {{ 'rootCauseGaps.identifiedAt' | t }} {{ g.identifiedAt | date: 'medium' }}
          @if (g.closedAt) {
            · {{ 'rootCauseGaps.closedAt' | t }} {{ g.closedAt | date: 'medium' }}
          }
        </p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        @if (canManage() && g.status === 'closed') {
          <button matButton type="button" (click)="reopen()">
            <app-icon name="refresh" />
            {{ 'rootCauseGaps.actions.reopen' | t }}
          </button>
        }
        <button matButton type="button" (click)="doClose()">{{ 'rootCauseGaps.actions.done' | t }}</button>
      </mat-dialog-actions>
    }
  `,
  styles: `
    h2 {
      display: flex;
      align-items: center;
      gap: 0.6rem;
    }
    .desc {
      margin: 0 0 0.75rem;
    }
    .closure {
      margin: 0 0 0.75rem;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .section-title {
      margin: 1rem 0 0.5rem;
      font-size: 0.95rem;
    }
    .add-row,
    .close-row {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
      min-width: 460px;
    }
    .grow,
    .full {
      flex: 1;
      width: 100%;
    }
    .links {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
    }
    .links__item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      padding: 0.4rem 0.6rem;
      border-radius: 8px;
      background: var(--mat-sys-surface-container-low);
    }
    .links__body {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .links__title {
      font-weight: 500;
    }
    .links__meta {
      font: var(--mat-sys-label-small);
    }
    .rem-add {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      min-width: 460px;
    }
    .rem-add__row {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
    }
    .rem-actions {
      display: flex;
      gap: 0.15rem;
      flex: 0 0 auto;
    }
    .rem--done .links__title {
      text-decoration: line-through;
      color: var(--mat-sys-on-surface-variant);
    }
    .meta {
      margin-top: 1rem;
      font: var(--mat-sys-label-small);
    }
    @media (max-width: 560px) {
      .add-row,
      .close-row {
        min-width: auto;
        flex-direction: column;
      }
    }
  `,
})
export class RootCauseGapDetailDialogComponent {
  readonly data = inject<RootCauseGapDetailDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<RootCauseGapDetailDialogComponent, boolean>>(MatDialogRef);
  private readonly service = inject(RootCauseGapsService);
  private readonly exceptions = inject(ExceptionsService);
  private readonly usersService = inject(UsersService);
  private readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly gap = signal<RootCauseGap | null>(null);
  readonly allFindings = signal<ExceptionListItem[]>([]);
  readonly users = signal<UserDto[]>([]);
  /** True if any mutation happened, so the register refreshes on close. */
  private changed = false;

  readonly canManage = computed(() => this.auth.hasPermission(Permissions.ManageException));

  readonly availableFindings = computed(() => {
    const linked = new Set(this.gap()?.linkedExceptions.map((e) => e.exceptionId) ?? []);
    return this.allFindings().filter((e) => !linked.has(e.id));
  });

  readonly linkForm = this.fb.nonNullable.group({ exceptionId: [''] });
  readonly closeForm = this.fb.nonNullable.group({ rationale: [''] });
  readonly remediationForm = this.fb.nonNullable.group({
    description: [''],
    ownerUserId: [''],
    dueDate: [''],
  });

  constructor() {
    this.refresh();
    // Load open findings for the link picker (load-all cap).
    this.exceptions.list({ status: 'open_any', pageSize: 0 }).subscribe((page) => this.allFindings.set(page.items));
    // Active users back the remediation-owner picker.
    this.usersService.list({ status: 'active', pageSize: 0 }).subscribe((page) => this.users.set(page.items));
  }

  private refresh(): void {
    this.service.getById(this.data.gapId).subscribe((g) => this.gap.set(g));
  }

  link(): void {
    const g = this.gap();
    const exceptionId = this.linkForm.controls.exceptionId.value;
    if (!g || !exceptionId) {
      return;
    }
    this.service.linkException(g.id, { exceptionId, version: g.version }).subscribe({
      next: (updated) => {
        this.gap.set(updated);
        this.linkForm.reset();
        this.changed = true;
        this.notify.success(this.i18n.translate('rootCauseGaps.notify.linked'));
      },
    });
  }

  unlink(exceptionId: string): void {
    const g = this.gap();
    if (!g) {
      return;
    }
    this.service.unlinkException(g.id, exceptionId, g.version).subscribe({
      next: (updated) => {
        this.gap.set(updated);
        this.changed = true;
        this.notify.success(this.i18n.translate('rootCauseGaps.notify.unlinked'));
      },
    });
  }

  close(): void {
    const g = this.gap();
    const rationale = this.closeForm.controls.rationale.value.trim();
    if (!g || rationale.length < 10) {
      return;
    }
    this.service.close(g.id, { rationale, version: g.version }).subscribe({
      next: (updated) => {
        this.gap.set(updated);
        this.changed = true;
        this.notify.success(this.i18n.translate('rootCauseGaps.notify.closed'));
      },
    });
  }

  reopen(): void {
    const g = this.gap();
    if (!g) {
      return;
    }
    this.service.reopen(g.id, g.version).subscribe({
      next: (updated) => {
        this.gap.set(updated);
        this.changed = true;
        this.notify.success(this.i18n.translate('rootCauseGaps.notify.reopened'));
      },
    });
  }

  addRemediation(): void {
    const g = this.gap();
    const v = this.remediationForm.getRawValue();
    if (!g || !v.description.trim() || !v.ownerUserId) {
      return;
    }
    this.service
      .addRemediation(g.id, {
        description: v.description.trim(),
        ownerUserId: v.ownerUserId,
        dueDate: v.dueDate || null,
        version: g.version,
      })
      .subscribe({
        next: (updated) => {
          this.gap.set(updated);
          this.remediationForm.reset();
          this.changed = true;
          this.notify.success(this.i18n.translate('rootCauseGaps.remediation.notify.added'));
        },
      });
  }

  completeRemediation(r: RootCauseGapRemediation): void {
    const g = this.gap();
    if (!g) {
      return;
    }
    this.service.completeRemediation(g.id, r.id, { note: null, version: g.version }).subscribe({
      next: (updated) => {
        this.gap.set(updated);
        this.changed = true;
        this.notify.success(this.i18n.translate('rootCauseGaps.remediation.notify.completed'));
      },
    });
  }

  reopenRemediation(r: RootCauseGapRemediation): void {
    const g = this.gap();
    if (!g) {
      return;
    }
    this.service.reopenRemediation(g.id, r.id, g.version).subscribe({
      next: (updated) => {
        this.gap.set(updated);
        this.changed = true;
      },
    });
  }

  removeRemediation(r: RootCauseGapRemediation): void {
    const g = this.gap();
    if (!g) {
      return;
    }
    this.service.removeRemediation(g.id, r.id, g.version).subscribe({
      next: (updated) => {
        this.gap.set(updated);
        this.changed = true;
        this.notify.success(this.i18n.translate('rootCauseGaps.remediation.notify.removed'));
      },
    });
  }

  nameOf(userId: string): string {
    return this.userLookup.displayName(userId);
  }

  /** Map a snake_case exception status to its (camelCase-keyed) label. */
  statusLabel(status: string): string {
    const key = 'exceptions.status.' + status.replace(/_([a-z])/g, (_, c) => c.toUpperCase());
    const label = this.i18n.translate(key);
    return label === key ? status.replace(/_/g, ' ') : label;
  }

  doClose(): void {
    this.dialogRef.close(this.changed);
  }
}
