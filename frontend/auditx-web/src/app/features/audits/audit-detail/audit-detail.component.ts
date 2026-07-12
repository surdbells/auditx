import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  CdkDrag,
  CdkDragHandle,
  CdkDropList,
  type CdkDragDrop,
  moveItemInArray,
} from '@angular/cdk/drag-drop';
import { Observable } from 'rxjs';

import { RouterLink } from '@angular/router';

import { AuditsService } from '../../../core/services/audits.service';
import { ExceptionsService } from '../../../core/services/exceptions.service';
import { TemplatesService } from '../../../core/services/templates.service';
import { AnnualPlansService } from '../../../core/services/annual-plans.service';
import { UsersService } from '../../../core/services/users.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { Permissions } from '../../../core/permissions';
import {
  Audit,
  AuditChecklistItem,
  AuditHistoryEntry,
  AuditTeamMember,
  ExceptionListItem,
  PlanItemLocator,
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
  SectionNameDialogComponent,
  SectionNameDialogData,
  SectionNameDialogResult,
} from '../dialogs/section-name-dialog.component';
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
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the audit-detail engagement view (walkthrough + About panel). */
const AUDIT_DETAIL_GUIDE: PageGuide = {
  id: 'audit-detail',
  titleKey: 'audits.detail.guide.pageTitle',
  purposeKey: 'audits.detail.guide.purpose',
  descriptionKey: 'audits.detail.guide.description',
  actionKeys: [
    'audits.detail.guide.action.transition',
    'audits.detail.guide.action.team',
    'audits.detail.guide.action.checklist',
    'audits.detail.guide.action.fieldwork',
  ],
  sections: [
    { selector: '.detail__actions', titleKey: 'audits.detail.guide.section.lifecycle.title', bodyKey: 'audits.detail.guide.section.lifecycle.body' },
    { selector: '[data-guide="overview"]', titleKey: 'audits.detail.guide.section.overview.title', bodyKey: 'audits.detail.guide.section.overview.body' },
    { selector: '[data-guide="team"]', titleKey: 'audits.detail.guide.section.team.title', bodyKey: 'audits.detail.guide.section.team.body' },
    { selector: '[data-guide="checklist"]', titleKey: 'audits.detail.guide.section.checklist.title', bodyKey: 'audits.detail.guide.section.checklist.body' },
  ],
  workflowKeys: [
    'audits.detail.guide.flow.create',
    'audits.detail.guide.flow.draft',
    'audits.detail.guide.flow.plan',
    'audits.detail.guide.flow.execute',
    'audits.detail.guide.flow.review',
  ],
  dependsOnKeys: [
    'audits.detail.guide.dep.template',
    'audits.detail.guide.dep.users',
    'audits.detail.guide.dep.plan',
    'audits.detail.guide.dep.refdata',
  ],
  usedByKeys: [
    'audits.detail.guide.use.fieldwork',
    'audits.detail.guide.use.exceptions',
    'audits.detail.guide.use.reports',
    'audits.detail.guide.use.analytics',
  ],
  businessRuleKeys: [
    'audits.detail.guide.rule.plan',
    'audits.detail.guide.rule.checklist',
    'audits.detail.guide.rule.review',
    'audits.detail.guide.rule.readonly',
  ],
  tipKeys: [
    'audits.detail.guide.tip.drag',
    'audits.detail.guide.tip.transfer',
    'audits.detail.guide.tip.timeline',
  ],
  permissionKeys: [
    'audits.detail.guide.perm.manage',
    'audits.detail.guide.perm.report',
    'audits.detail.guide.perm.exceptions',
  ],
  faq: [
    { questionKey: 'audits.detail.guide.faq.plan.q', answerKey: 'audits.detail.guide.faq.plan.a' },
    { questionKey: 'audits.detail.guide.faq.findings.q', answerKey: 'audits.detail.guide.faq.findings.a' },
  ],
};

/** A checklist section grouping for the template. */
interface ChecklistGroup {
  /** Display name (ungrouped uses a translated label). */
  name: string;
  /** The section's real name for CRUD; null for the ungrouped bucket. */
  sectionName: string | null;
  items: AuditChecklistItem[];
}

const CONCURRENCY_CONFLICT = 'audit.concurrency_conflict';

@Component({
  selector: 'app-audit-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    NgTemplateOutlet,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    MatChipsModule,
    MatTooltipModule,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
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
  private readonly plans = inject(AnnualPlansService);
  /** Active users for the team/checklist assignment pickers (assignable = active). */
  private readonly users = inject(UsersService);
  /** Directory-backed user-name resolver for display (all users, no admin permission). */
  readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);
  /** Resolves the stored audit-type code to its human label (lazy-loaded). */
  readonly refLookup = inject(ReferenceDataLookupService);

  /** Contextual page guide (walkthrough + About panel). */
  readonly guide = AUDIT_DETAIL_GUIDE;

  readonly state = signal<ViewState>('loading');
  readonly audit = signal<Audit | null>(null);
  /** Resolved template name for the metadata card; falls back to the id. */
  readonly templateName = signal<string | null>(null);
  /** Tracks which templateId `templateName` was resolved for (avoids refetch). */
  private resolvedTemplateId: string | null = null;
  /** The annual-plan item this audit fulfils, resolved for the deep link (null until/unless resolved). */
  readonly planLink = signal<PlanItemLocator | null>(null);
  private resolvedPlanItemId: string | null = null;
  /** Exceptions raised against this audit (M6). */
  readonly exceptions = signal<ExceptionListItem[]>([]);
  /** The audit's activity timeline (lifecycle / team / section / checklist events). */
  readonly activity = signal<AuditHistoryEntry[]>([]);

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

  readonly canViewPlan = computed(() =>
    this.auth.hasPermission(Permissions.ViewPlan),
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

  /**
   * Checklist grouped into first-class sections (ordered by the section's own OrderIndex, empty sections
   * included), followed by any orphan sections (safety net) and an "ungrouped" bucket for items with no section.
   */
  readonly checklistGroups = computed<ChecklistGroup[]>(() => {
    const audit = this.audit();
    const items = [...(audit?.checklistItems ?? [])].sort(
      (a, b) => a.orderIndex - b.orderIndex,
    );
    const sections = [...(audit?.sections ?? [])].sort(
      (a, b) => a.orderIndex - b.orderIndex,
    );

    const itemsBySection = new Map<string, AuditChecklistItem[]>();
    const ungrouped: AuditChecklistItem[] = [];
    for (const item of items) {
      const key = item.sectionName?.trim();
      if (!key) {
        ungrouped.push(item);
        continue;
      }
      const list = itemsBySection.get(key.toLowerCase()) ?? [];
      list.push(item);
      itemsBySection.set(key.toLowerCase(), list);
    }

    const groups: ChecklistGroup[] = [];
    const seen = new Set<string>();
    for (const s of sections) {
      const key = s.name.toLowerCase();
      seen.add(key);
      groups.push({ name: s.name, sectionName: s.name, items: itemsBySection.get(key) ?? [] });
    }
    // Orphan section names that have no entity (shouldn't happen post-migration, but never drop items).
    for (const [key, list] of itemsBySection) {
      if (!seen.has(key)) {
        const name = list[0].sectionName!.trim();
        groups.push({ name, sectionName: name, items: list });
      }
    }
    if (ungrouped.length) {
      groups.push({
        name: this.i18n.translate('audits.checklist.ungrouped'),
        sectionName: null,
        items: ungrouped,
      });
    }
    return groups;
  });

  /** Section names for the item dialog's section picker. */
  readonly sectionNames = computed(() =>
    [...(this.audit()?.sections ?? [])]
      .sort((a, b) => a.orderIndex - b.orderIndex)
      .map((s) => s.name),
  );

  /** Entity-backed sections (draggable/reorderable); ungrouped is pinned separately. */
  readonly entitySectionGroups = computed(() =>
    this.checklistGroups().filter((g) => g.sectionName !== null),
  );
  readonly ungroupedGroup = computed(() =>
    this.checklistGroups().find((g) => g.sectionName === null) ?? null,
  );
  /** cdkDropList ids so item lists connect for cross-section drag. */
  readonly connectedListIds = computed(() =>
    this.checklistGroups().map((g) => this.listId(g)),
  );

  listId(group: ChecklistGroup): string {
    return (
      'cl-' +
      (group.sectionName
        ? group.sectionName.toLowerCase().replace(/[^a-z0-9]+/g, '-')
        : '__ungrouped__')
    );
  }

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
      missing.push(this.i18n.translate('audits.planHint.checklistItem'));
    }
    if (this.auditorCount() < 1) {
      missing.push(this.i18n.translate('audits.planHint.auditor'));
    }
    const items = missing.join(
      ` ${this.i18n.translate('audits.planHint.and')} `,
    );
    return this.i18n.translate('audits.planHint.template', { items });
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

  /** Sections may be added / renamed / removed / reordered while the checklist is editable (draft or in progress). */
  readonly canManageSections = computed(
    () => this.canManage() && (this.isDraft() || this.isInProgress()),
  );

  /** Items may be reordered / moved across sections in draft OR in progress (unlike edit/remove, which are draft-only). */
  readonly canReorderChecklist = computed(
    () => this.canManage() && (this.isDraft() || this.isInProgress()),
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
        this.resolvePlanLink(audit.planItemId);
        this.loadExceptions();
        this.loadActivity();
      },
      error: () => this.state.set('error'),
    });
  }

  /** Loads the audit's activity timeline (non-fatal on error). */
  private loadActivity(): void {
    this.service.getHistory(this.id()).subscribe({
      next: (entries) => this.activity.set(entries),
      error: () => {
        // Non-fatal: leave the timeline empty.
      },
    });
  }

  /**
   * Resolves the audit's plan item to its owning plan for the deep link (once per id, and only when the user can
   * view plans). Non-fatal: on error the card shows a plain "linked to the annual plan" label without a link.
   */
  private resolvePlanLink(planItemId: string | null | undefined): void {
    if (!planItemId || !this.canViewPlan()) {
      this.planLink.set(null);
      this.resolvedPlanItemId = null;
      return;
    }
    if (planItemId === this.resolvedPlanItemId) {
      return;
    }
    this.resolvedPlanItemId = planItemId;
    this.plans.planItemLocator(planItemId).subscribe({
      next: (locator) => this.planLink.set(locator),
      error: () => {
        // Non-fatal: fall back to a plain "linked to the annual plan" label.
      },
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
        this.loadActivity();
      },
    });
  }

  /** Loads the active-user list (for the team/checklist pickers) and warms the display-name directory. */
  private ensureUsers(): void {
    this.userLookup.ensureLoaded();
    if (this.usersCache.length) {
      return;
    }
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
      },
      error: () => {
        // Non-fatal: the pickers fall back to empty; names still resolve via the directory.
      },
    });
  }

  nameOf(userId: string | null | undefined): string {
    return this.userLookup.displayName(userId);
  }

  /* ---- Activity timeline ---- */

  private static readonly EVENT_LABELS: Record<string, string> = {
    audit_created: 'audits.activity.created',
    audit_metadata_updated: 'audits.activity.metadataUpdated',
    audit_completed: 'audits.activity.completed',
    audit_cancelled: 'audits.activity.cancelled',
    audit_team_member_added: 'audits.activity.teamAdded',
    audit_team_member_removed: 'audits.activity.teamRemoved',
    audit_lead_transferred: 'audits.activity.leadTransferred',
    audit_checklist_item_added: 'audits.activity.itemAdded',
    audit_checklist_item_edited: 'audits.activity.itemEdited',
    audit_checklist_item_removed: 'audits.activity.itemRemoved',
    audit_checklist_items_reordered: 'audits.activity.itemsReordered',
    audit_section_added: 'audits.activity.sectionAdded',
    audit_section_renamed: 'audits.activity.sectionRenamed',
    audit_section_removed: 'audits.activity.sectionRemoved',
    audit_sections_reordered: 'audits.activity.sectionsReordered',
  };

  private parseState(json: string | null): { status?: string; reason?: string } | null {
    if (!json) {
      return null;
    }
    try {
      return JSON.parse(json) as { status?: string; reason?: string };
    } catch {
      return null;
    }
  }

  eventLabel(entry: AuditHistoryEntry): string {
    if (entry.eventType === 'audit_transitioned') {
      const state = this.parseState(entry.stateJson);
      const status = state?.status;
      const hasReason = !!state?.reason;
      const key =
        status === 'draft'
          ? 'reopened'
          : status === 'planned'
            ? 'planned'
            : status === 'in_progress'
              ? hasReason
                ? 'returned'
                : 'started'
              : status === 'under_review'
                ? 'sentReview'
                : status === 'completed'
                  ? 'completed'
                  : status === 'cancelled'
                    ? 'cancelled'
                    : 'transitioned';
      return this.i18n.translate('audits.activity.' + key);
    }
    const labelKey = AuditDetailComponent.EVENT_LABELS[entry.eventType];
    return labelKey
      ? this.i18n.translate(labelKey)
      : entry.eventType.replace(/_/g, ' ');
  }

  /** The reason attached to a transition (e.g. the reopen justification), if any. */
  reasonOf(entry: AuditHistoryEntry): string | null {
    const reason = this.parseState(entry.stateJson)?.reason;
    return reason && reason.trim() ? reason.trim() : null;
  }

  isReopen(entry: AuditHistoryEntry): boolean {
    return (
      entry.eventType === 'audit_transitioned' &&
      this.parseState(entry.stateJson)?.status === 'draft'
    );
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
    this.transitionTo('planned', this.i18n.translate('audits.notify.planned'));
  }

  start(): void {
    this.transitionTo('in_progress', this.i18n.translate('audits.notify.started'));
  }

  complete(): void {
    this.transitionTo('completed', this.i18n.translate('audits.notify.completed'));
  }

  reopen(): void {
    this.transitionWithReason(
      'draft',
      {
        title: this.i18n.translate('audits.reopen.title'),
        message: this.i18n.translate('audits.reopen.message'),
        reasonRequired: true,
        confirmLabel: this.i18n.translate('audits.actions.reopen'),
      },
      this.i18n.translate('audits.notify.reopened'),
    );
  }

  returnToInProgress(): void {
    this.transitionWithReason(
      'in_progress',
      {
        title: this.i18n.translate('audits.returnToProgress.title'),
        message: this.i18n.translate('audits.returnToProgress.message'),
        reasonRequired: true,
        confirmLabel: this.i18n.translate('audits.actions.return'),
      },
      this.i18n.translate('audits.notify.returned'),
    );
  }

  sendToReview(): void {
    // A reason is required only when there are still unanswered items.
    if (this.openItemCount() > 0) {
      this.transitionWithReason(
        'under_review',
        {
          title: this.i18n.translate('audits.sendReview.title'),
          message: this.i18n.translate('audits.sendReview.message', {
            count: this.openItemCount(),
          }),
          reasonRequired: true,
          confirmLabel: this.i18n.translate('audits.actions.sendToReview'),
        },
        this.i18n.translate('audits.notify.sentForReview'),
      );
      return;
    }
    this.transitionTo(
      'under_review',
      this.i18n.translate('audits.notify.sentForReview'),
    );
  }

  cancel(): void {
    this.dialog
      .open(TransitionReasonDialogComponent, {
        data: {
          title: this.i18n.translate('audits.cancel.title'),
          message: this.i18n.translate('audits.cancel.message'),
          reasonRequired: true,
          confirmLabel: this.i18n.translate('audits.cancel.confirm'),
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
          this.i18n.translate('audits.notify.cancelled'),
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
          this.i18n.translate('audits.notify.memberAdded'),
        );
      });
  }

  removeMember(member: AuditTeamMember): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('audits.removeMember.title'),
      message: this.i18n.translate('audits.removeMember.message', {
        name: this.nameOf(member.userId),
      }),
      confirmLabel: this.i18n.translate('audits.actions.remove'),
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
              this.notify.success(
                this.i18n.translate('audits.notify.memberRemoved'),
              );
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
          this.i18n.translate('audits.notify.leadTransferred'),
        );
      });
  }

  /* ---- Sections (first-class CRUD) ---- */

  addSection(): void {
    const data: SectionNameDialogData = {
      title: this.i18n.translate('audits.sections.addTitle'),
      label: this.i18n.translate('audits.sections.nameLabel'),
      confirmLabel: this.i18n.translate('audits.sections.addConfirm'),
      cancelLabel: this.i18n.translate('common.cancel'),
      duplicateError: this.i18n.translate('audits.sections.duplicate'),
      requiredError: this.i18n.translate('audits.sections.required'),
      existingNames: this.sectionNames(),
    };
    this.dialog
      .open(SectionNameDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((result?: SectionNameDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.addSection(this.id(), { name: result.name, version: this.version() }),
          this.i18n.translate('audits.sections.added'),
        );
      });
  }

  renameSection(group: ChecklistGroup): void {
    if (!group.sectionName) {
      return;
    }
    const current = group.sectionName;
    const data: SectionNameDialogData = {
      title: this.i18n.translate('audits.sections.renameTitle'),
      label: this.i18n.translate('audits.sections.nameLabel'),
      confirmLabel: this.i18n.translate('audits.sections.renameConfirm'),
      cancelLabel: this.i18n.translate('common.cancel'),
      duplicateError: this.i18n.translate('audits.sections.duplicate'),
      requiredError: this.i18n.translate('audits.sections.required'),
      initialName: current,
      existingNames: this.sectionNames(),
    };
    this.dialog
      .open(SectionNameDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((result?: SectionNameDialogResult) => {
        if (!result || result.name === current) {
          return;
        }
        this.runMutation(
          this.service.renameSection(this.id(), {
            currentName: current,
            newName: result.name,
            version: this.version(),
          }),
          this.i18n.translate('audits.sections.renamed'),
        );
      });
  }

  removeSection(group: ChecklistGroup): void {
    if (!group.sectionName) {
      return;
    }
    const name = group.sectionName;
    const data: ConfirmDialogData = {
      title: this.i18n.translate('audits.sections.removeTitle'),
      message: this.i18n.translate('audits.sections.removeMessage', { name }),
      confirmLabel: this.i18n.translate('audits.actions.remove'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.runMutation(
          this.service.removeSection(this.id(), { name, version: this.version() }),
          this.i18n.translate('audits.sections.removed'),
        );
      });
  }

  /* ---- Checklist ---- */

  addChecklistItem(): void {
    const data: ChecklistItemDialogData = { users: this.usersCache, sections: this.sectionNames() };
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
            responseConfigJson: result.responseConfigJson,
            sectionName: result.sectionName,
            isRequired: result.isRequired,
            assignedUserId: result.assignedUserId,
            version: this.version(),
          }),
          this.i18n.translate('audits.notify.itemAdded'),
        );
      });
  }

  editChecklistItem(item: AuditChecklistItem): void {
    const data: ChecklistItemDialogData = { item, users: this.usersCache, sections: this.sectionNames() };
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
            responseType: result.responseType,
            responseConfigJson: result.responseConfigJson,
            sectionName: result.sectionName,
            isRequired: result.isRequired,
            assignedUserId: result.assignedUserId,
            version: this.version(),
          }),
          this.i18n.translate('audits.notify.itemUpdated'),
        );
      });
  }

  removeChecklistItem(item: AuditChecklistItem): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('audits.removeItem.title'),
      message: this.i18n.translate('audits.removeItem.message'),
      confirmLabel: this.i18n.translate('audits.actions.remove'),
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
              this.notify.success(
                this.i18n.translate('audits.notify.itemRemoved'),
              );
              this.reload();
            },
            error: (err: unknown) => this.handleMutationError(err),
          });
      });
  }

  /**
   * Drag a checklist item — within a section (reorder) or across sections (move). Rebuilds the full ordered
   * placement list (item + its destination section), optimistically re-stamps order + section so the row
   * doesn't snap back, sends one arrange call, and reverts on error. Works in draft and in progress.
   */
  dropChecklistItem(event: CdkDragDrop<AuditChecklistItem[]>): void {
    const current = this.audit();
    if (!current) {
      return;
    }
    const groups = this.checklistGroups();
    const source = groups.find((g) => this.listId(g) === event.previousContainer.id);
    const target = groups.find((g) => this.listId(g) === event.container.id);
    if (!source || !target) {
      return;
    }
    if (source === target && event.previousIndex === event.currentIndex) {
      return;
    }

    // Mutable copies keyed by list id; move the dragged item.
    const arrays = new Map(groups.map((g) => [this.listId(g), [...g.items]]));
    const [moved] = arrays.get(this.listId(source))!.splice(event.previousIndex, 1);
    arrays.get(this.listId(target))!.splice(event.currentIndex, 0, moved);

    // Full ordered placement list + optimistic re-stamp (order + destination section).
    const placements: { itemId: string; sectionName: string | null }[] = [];
    const restamp = new Map<string, { order: number; section: string | null }>();
    let order = 0;
    for (const g of groups) {
      for (const item of arrays.get(this.listId(g))!) {
        placements.push({ itemId: item.id, sectionName: g.sectionName });
        restamp.set(item.id, { order: order++, section: g.sectionName });
      }
    }
    this.audit.set({
      ...current,
      checklistItems: current.checklistItems.map((it) => {
        const r = restamp.get(it.id);
        return r ? { ...it, orderIndex: r.order, sectionName: r.section } : it;
      }),
    });

    this.service
      .arrangeChecklistItems(current.id, { placements, version: current.version })
      .subscribe({
        next: (updated) => this.audit.set(updated),
        error: (err: unknown) => {
          this.reload();
          this.handleMutationError(err);
        },
      });
  }

  /** Reorder whole sections by dragging. Only entity-backed sections participate (ungrouped is pinned). */
  dropSection(event: CdkDragDrop<ChecklistGroup[]>): void {
    if (event.previousIndex === event.currentIndex) {
      return;
    }
    const current = this.audit();
    if (!current) {
      return;
    }
    const groups = [...this.entitySectionGroups()];
    moveItemInArray(groups, event.previousIndex, event.currentIndex);
    const orderedSectionNames = groups
      .map((g) => g.sectionName)
      .filter((n): n is string => n !== null);

    // Optimistic re-stamp of section order.
    const rank = new Map(orderedSectionNames.map((n, i) => [n.toLowerCase(), i]));
    this.audit.set({
      ...current,
      sections: current.sections.map((s) => ({
        ...s,
        orderIndex: rank.get(s.name.toLowerCase()) ?? s.orderIndex,
      })),
    });

    this.service
      .reorderSections(current.id, { orderedSectionNames, version: current.version })
      .subscribe({
        next: (updated) => this.audit.set(updated),
        error: (err: unknown) => {
          this.reload();
          this.handleMutationError(err);
        },
      });
  }
}
