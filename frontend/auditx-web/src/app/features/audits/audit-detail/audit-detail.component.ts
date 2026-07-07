import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable } from 'rxjs';

import { RouterLink } from '@angular/router';

import { AuditsService } from '../../../core/services/audits.service';
import { ExceptionsService } from '../../../core/services/exceptions.service';
import { TemplatesService } from '../../../core/services/templates.service';
import { UsersService } from '../../../core/services/users.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { Permissions } from '../../../core/permissions';
import {
  Audit,
  AuditChecklistItem,
  AuditTeamMember,
  ExceptionListItem,
  ProblemDetails,
  TransitionTarget,
  UserDto,
} from '../../../core/models';
import {
  ChecklistItemDialogComponent,
  ChecklistItemDialogData,
  ChecklistItemDialogResult,
} from '../dialogs/checklist-item-dialog.component';
import {
  AddTeamMemberDialogComponent,
  AddTeamMemberDialogData,
  AddTeamMemberDialogResult,
} from '../dialogs/add-team-member-dialog.component';
import {
  TransferLeadDialogComponent,
  TransferLeadDialogData,
  TransferLeadDialogResult,
} from '../dialogs/transfer-lead-dialog.component';
import {
  TransitionReasonDialogComponent,
  TransitionReasonDialogData,
  TransitionReasonResult,
} from '../dialogs/transition-reason-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AuditExecutionComponent } from '../audit-execution/audit-execution.component';

type ViewState = 'loading' | 'ready' | 'error';

/** A checklist section grouping for the template. */
interface ChecklistGroup {
  name: string;
  items: AuditChecklistItem[];
}

const CONCURRENCY_CONFLICT = 'audit.concurrency_conflict';

@Component({
  selector: 'app-audit-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    AuditExecutionComponent,
  ],
  templateUrl: './audit-detail.component.html',
  styleUrl: './audit-detail.component.scss',
})
export class AuditDetailComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(AuditsService);
  private readonly exceptionsService = inject(ExceptionsService);
  private readonly templates = inject(TemplatesService);
  private readonly users = inject(UsersService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  /** Resolves the stored audit-type code to its human label (lazy-loaded). */
  readonly refLookup = inject(ReferenceDataLookupService);

  readonly state = signal<ViewState>('loading');
  readonly audit = signal<Audit | null>(null);
  /** Resolved template name for the metadata card; falls back to the id. */
  readonly templateName = signal<string | null>(null);
  /** Tracks which templateId `templateName` was resolved for (avoids refetch). */
  private resolvedTemplateId: string | null = null;
  /** userId → display name, resolved lazily. */
  readonly userNames = signal<Record<string, string>>({});
  /** Exceptions raised against this audit (M6). */
  readonly exceptions = signal<ExceptionListItem[]>([]);

  private usersCache: UserDto[] = [];

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageAudit),
  );

  readonly canViewExceptions = computed(() =>
    this.auth.hasPermission(Permissions.ViewExceptions),
  );

  readonly canViewReports = computed(() =>
    this.auth.hasPermission(Permissions.ViewReport),
  );

  readonly status = computed(() => this.audit()?.status ?? null);

  readonly leadName = computed(() => this.nameOf(this.audit()?.leadUserId));
  readonly auditeeName = computed(() =>
    this.nameOf(this.audit()?.auditeeUserId),
  );

  /** Active members only, for display. */
  readonly activeMembers = computed(() =>
    (this.audit()?.teamMembers ?? []).filter((m) => m.isActive),
  );

  /** Checklist grouped by sectionName, ordered by orderIndex. */
  readonly checklistGroups = computed<ChecklistGroup[]>(() => {
    const items = [...(this.audit()?.checklistItems ?? [])].sort(
      (a, b) => a.orderIndex - b.orderIndex,
    );
    const groups: ChecklistGroup[] = [];
    const byName = new Map<string, ChecklistGroup>();
    for (const item of items) {
      const name = item.sectionName?.trim() || 'Ungrouped';
      let group = byName.get(name);
      if (!group) {
        group = { name, items: [] };
        byName.set(name, group);
        groups.push(group);
      }
      group.items.push(item);
    }
    return groups;
  });

  private readonly auditorCount = computed(
    () =>
      this.activeMembers().filter((m) => m.teamRole === 'auditor').length,
  );

  private readonly checklistCount = computed(
    () => this.audit()?.checklistItems.length ?? 0,
  );

  /** Open (unanswered) checklist items. */
  readonly openItemCount = computed(
    () =>
      (this.audit()?.checklistItems ?? []).filter(
        (i) => i.itemState !== 'responded',
      ).length,
  );

  /* ---- Transition availability ---- */

  readonly canPlan = computed(
    () => this.checklistCount() >= 1 && this.auditorCount() >= 1,
  );

  readonly planHint = computed(() => {
    if (this.canPlan()) {
      return '';
    }
    const missing: string[] = [];
    if (this.checklistCount() < 1) {
      missing.push('at least one checklist item');
    }
    if (this.auditorCount() < 1) {
      missing.push('at least one auditor');
    }
    return `Add ${missing.join(' and ')} before planning.`;
  });

  readonly isDraft = computed(() => this.status() === 'draft');
  readonly isPlanned = computed(() => this.status() === 'planned');

  /** Execution / fieldwork is shown once the audit has moved past Draft. */
  readonly showExecution = computed(
    () => this.status() !== null && this.status() !== 'draft',
  );
  readonly isInProgress = computed(() => this.status() === 'in_progress');
  readonly isUnderReview = computed(() => this.status() === 'under_review');
  readonly isCompleted = computed(() => this.status() === 'completed');
  readonly isCancelled = computed(() => this.status() === 'cancelled');
  readonly isReadOnly = computed(() => this.isCompleted() || this.isCancelled());

  /** Cancel is available in any non-terminal state. */
  readonly canCancel = computed(
    () => this.canManage() && !this.isReadOnly(),
  );

  /** Checklist items may be added in draft + in_progress. */
  readonly canAddChecklistItem = computed(
    () => this.canManage() && (this.isDraft() || this.isInProgress()),
  );

  /** Checklist items may be edited / removed only in draft. */
  readonly canEditChecklist = computed(
    () => this.canManage() && this.isDraft(),
  );

  /** Team may be changed while the audit is not read-only. */
  readonly canManageTeam = computed(
    () => this.canManage() && !this.isReadOnly(),
  );

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getById(this.id()).subscribe({
      next: (audit) => {
        this.audit.set(audit);
        this.state.set('ready');
        this.ensureUsers();
        this.resolveTemplateName(audit.templateId);
        this.loadExceptions();
      },
      error: () => this.state.set('error'),
    });
  }

  /**
   * Resolves the audit's template id to a human name (once per id). Leaves the
   * name null on failure or when there is no template, so the metadata card
   * falls back to the raw id / "—".
   */
  private resolveTemplateName(templateId: string | null | undefined): void {
    if (!templateId) {
      this.templateName.set(null);
      this.resolvedTemplateId = null;
      return;
    }
    if (templateId === this.resolvedTemplateId) {
      return;
    }
    this.resolvedTemplateId = templateId;
    this.templates.getById(templateId).subscribe({
      next: (template) => this.templateName.set(template.name),
      error: () => {
        // Non-fatal: the card falls back to the raw template id.
      },
    });
  }

  /** Loads the exceptions raised against this audit (M6). Non-fatal on error. */
  private loadExceptions(): void {
    if (!this.canViewExceptions()) {
      return;
    }
    this.exceptionsService.listForAudit(this.id()).subscribe({
      next: (items) => this.exceptions.set(items),
      error: () => {
        // Non-fatal: leave the section empty.
      },
    });
  }

  /**
   * Reloads the audit aggregate (and thus its fresh `version`). Public so the
   * execution child can request it after a version-bearing mutation.
   */
  reload(): void {
    this.service.getById(this.id()).subscribe({
      next: (audit) => {
        this.audit.set(audit);
        this.ensureUsers();
        this.resolveTemplateName(audit.templateId);
      },
    });
  }

  /** Loads the active users once so ids resolve to display names. */
  private ensureUsers(): void {
    if (this.usersCache.length) {
      this.indexUsers(this.usersCache);
      return;
    }
    this.users.list({ status: 'active', limit: 200 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        this.indexUsers(page.items);
      },
      error: () => {
        // Non-fatal: ids will display verbatim.
      },
    });
  }

  private indexUsers(users: UserDto[]): void {
    const map: Record<string, string> = {};
    for (const u of users) {
      map[u.id] = u.displayName;
    }
    this.userNames.set(map);
  }

  nameOf(userId: string | null | undefined): string {
    if (!userId) {
      return '—';
    }
    return this.userNames()[userId] ?? userId;
  }

  /** Returns the live version, or throws if the audit is not loaded. */
  private version(): string {
    return this.audit()!.version;
  }

  /**
   * Applies a fresh audit returned by a mutation, then runs an optional success
   * callback. On a concurrency conflict (409 + audit.concurrency_conflict), the
   * global interceptor already toasts; we additionally reload for a fresh version.
   */
  private runMutation(
    op: Observable<Audit>,
    successMessage: string,
  ): void {
    op.subscribe({
      next: (audit) => {
        this.audit.set(audit);
        this.ensureUsers();
        this.notify.success(successMessage);
      },
      error: (err: unknown) => this.handleMutationError(err),
    });
  }

  private handleMutationError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 409) {
      const problem = err.error as ProblemDetails | null;
      if (problem?.error_code === CONCURRENCY_CONFLICT) {
        this.reload();
      }
    }
  }

  /* ---- Transitions ---- */

  private transitionTo(target: TransitionTarget, message: string): void {
    this.runMutation(
      this.service.transition(this.id(), {
        targetState: target,
        version: this.version(),
      }),
      message,
    );
  }

  private transitionWithReason(
    target: TransitionTarget,
    dialogData: TransitionReasonDialogData,
    message: string,
  ): void {
    this.dialog
      .open(TransitionReasonDialogComponent, {
        data: dialogData,
        width: '480px',
      })
      .afterClosed()
      .subscribe((result?: TransitionReasonResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.transition(this.id(), {
            targetState: target,
            reason: result.reason || null,
            version: this.version(),
          }),
          message,
        );
      });
  }

  plan(): void {
    if (!this.canPlan()) {
      return;
    }
    this.transitionTo('planned', 'Audit planned.');
  }

  start(): void {
    this.transitionTo('in_progress', 'Audit started.');
  }

  complete(): void {
    this.transitionTo('completed', 'Audit completed.');
  }

  reopen(): void {
    this.transitionWithReason(
      'draft',
      {
        title: 'Reopen audit',
        message: 'Move this planned audit back to draft.',
        reasonRequired: true,
        confirmLabel: 'Reopen',
      },
      'Audit reopened.',
    );
  }

  returnToInProgress(): void {
    this.transitionWithReason(
      'in_progress',
      {
        title: 'Return to in progress',
        message: 'Send this audit back to the team for more work.',
        reasonRequired: true,
        confirmLabel: 'Return',
      },
      'Audit returned to in progress.',
    );
  }

  sendToReview(): void {
    // A reason is required only when there are still unanswered items.
    if (this.openItemCount() > 0) {
      this.transitionWithReason(
        'under_review',
        {
          title: 'Send to review',
          message: `${this.openItemCount()} item(s) are still unanswered. Provide a reason to proceed.`,
          reasonRequired: true,
          confirmLabel: 'Send to review',
        },
        'Audit sent for review.',
      );
      return;
    }
    this.transitionTo('under_review', 'Audit sent for review.');
  }

  cancel(): void {
    this.dialog
      .open(TransitionReasonDialogComponent, {
        data: {
          title: 'Cancel audit',
          message: 'Cancelling is permanent. Provide a reason.',
          reasonRequired: true,
          confirmLabel: 'Cancel audit',
          destructive: true,
        } satisfies TransitionReasonDialogData,
        width: '480px',
      })
      .afterClosed()
      .subscribe((result?: TransitionReasonResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.cancel(this.id(), {
            reason: result.reason,
            version: this.version(),
          }),
          'Audit cancelled.',
        );
      });
  }

  /* ---- Team ---- */

  addMember(): void {
    const data: AddTeamMemberDialogData = { users: this.usersCache };
    this.dialog
      .open(AddTeamMemberDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: AddTeamMemberDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.addTeamMember(this.id(), {
            userId: result.userId,
            teamRole: result.teamRole,
            version: this.version(),
          }),
          'Team member added.',
        );
      });
  }

  removeMember(member: AuditTeamMember): void {
    const data: ConfirmDialogData = {
      title: 'Remove team member',
      message: `Remove ${this.nameOf(member.userId)} from the team?`,
      confirmLabel: 'Remove',
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.service
          .removeTeamMember(this.id(), member.id, this.version())
          .subscribe({
            next: () => {
              this.notify.success('Team member removed.');
              this.reload();
            },
            error: (err: unknown) => this.handleMutationError(err),
          });
      });
  }

  transferLead(): void {
    const candidates = this.activeMembers()
      .filter(
        (m) => m.teamRole === 'auditor' || m.teamRole === 'reviewer',
      )
      .map((member) => ({
        member,
        displayName: this.nameOf(member.userId),
      }));
    const data: TransferLeadDialogData = { candidates };
    this.dialog
      .open(TransferLeadDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: TransferLeadDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.transferLead(this.id(), {
            newLeadUserId: result.newLeadUserId,
            removeOutgoing: result.removeOutgoing,
            version: this.version(),
          }),
          'Lead transferred.',
        );
      });
  }

  /* ---- Checklist ---- */

  addChecklistItem(): void {
    const data: ChecklistItemDialogData = { users: this.usersCache };
    this.dialog
      .open(ChecklistItemDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: ChecklistItemDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.addChecklistItem(this.id(), {
            prompt: result.prompt,
            referenceNotes: result.referenceNotes,
            responseType: result.responseType,
            sectionName: result.sectionName,
            isRequired: result.isRequired,
            assignedUserId: result.assignedUserId,
            version: this.version(),
          }),
          'Checklist item added.',
        );
      });
  }

  editChecklistItem(item: AuditChecklistItem): void {
    const data: ChecklistItemDialogData = { item, users: this.usersCache };
    this.dialog
      .open(ChecklistItemDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: ChecklistItemDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.updateChecklistItem(this.id(), item.id, {
            prompt: result.prompt,
            referenceNotes: result.referenceNotes,
            sectionName: result.sectionName,
            isRequired: result.isRequired,
            assignedUserId: result.assignedUserId,
            version: this.version(),
          }),
          'Checklist item updated.',
        );
      });
  }

  removeChecklistItem(item: AuditChecklistItem): void {
    const data: ConfirmDialogData = {
      title: 'Remove checklist item',
      message: 'Remove this checklist item from the audit?',
      confirmLabel: 'Remove',
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.service
          .removeChecklistItem(this.id(), item.id, this.version())
          .subscribe({
            next: () => {
              this.notify.success('Checklist item removed.');
              this.reload();
            },
            error: (err: unknown) => this.handleMutationError(err),
          });
      });
  }
}
