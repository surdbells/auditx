import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable } from 'rxjs';

import { AuditsService } from '../../../core/services/audits.service';
import { ExceptionsService } from '../../../core/services/exceptions.service';
import { UsersService } from '../../../core/services/users.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  Audit,
  ChecklistProgress,
  ChecklistProgressItem,
  ChecklistResponse,
  EvidenceFile,
  ProblemDetails,
  RaiseExceptionRequest,
  ResponseVerdict,
  ReviewSummary,
  UserDto,
} from '../../../core/models';
import {
  RaiseExceptionDialogComponent,
  RaiseExceptionDialogData,
} from '../../exceptions/dialogs/raise-exception-dialog.component';
import {
  RespondItemDialogComponent,
  RespondItemDialogData,
  RespondItemDialogResult,
} from '../dialogs/respond-item-dialog.component';
import {
  AssignItemDialogComponent,
  AssignItemDialogData,
  AssignItemDialogResult,
  AssignableMember,
} from '../dialogs/assign-item-dialog.component';
import {
  FailJudgementDialogComponent,
  FailJudgementDialogData,
  FailJudgementDialogResult,
} from '../dialogs/fail-judgement-dialog.component';
import {
  ResponseHistoryDialogComponent,
  ResponseHistoryDialogData,
} from '../dialogs/response-history-dialog.component';
import {
  TransitionReasonDialogComponent,
  TransitionReasonDialogData,
  TransitionReasonResult,
} from '../dialogs/transition-reason-dialog.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AuditTimePanelComponent } from './audit-time-panel.component';
import { AuditProceduresPanelComponent } from './audit-procedures-panel.component';
import { AuditEvidencePanelComponent } from './audit-evidence-panel.component';

const CONCURRENCY_CONFLICT = 'audit.concurrency_conflict';

/** A progress item grouped under a section heading. */
interface ProgressGroup {
  name: string;
  items: ChecklistProgressItem[];
}

/**
 * Execution / fieldwork surface for an audit that is past Draft.
 *
 * Owns the checklist-progress, review-summary, per-item responses and evidence.
 * Version-bearing mutations (respond, discard, assign, fail-judgement) emit
 * {@link reloadRequested} so the parent refreshes the audit (and thus `version`);
 * after each the component also reloads progress + summary itself.
 */
@Component({
  selector: 'app-audit-execution',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe,
    DatePipe,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    MatChipsModule,
    MatTooltipModule,
    MatProgressBarModule,
    MatButtonToggleModule,
    MatTabsModule,
    AuditTimePanelComponent,
    AuditProceduresPanelComponent,
    AuditEvidencePanelComponent,
  ],
  templateUrl: './audit-execution.component.html',
  styleUrl: './audit-execution.component.scss',
})
export class AuditExecutionComponent {
  /** The current audit aggregate (re-supplied by the parent after each reload). */
  readonly audit = input.required<Audit>();

  /** Emitted after a version-bearing mutation so the parent reloads the audit. */
  readonly reloadRequested = output<void>();

  private readonly service = inject(AuditsService);
  private readonly exceptions = inject(ExceptionsService);
  /** Active users for the raise-exception picker (assignable = active). */
  private readonly users = inject(UsersService);
  /** Directory-backed user-name resolver for display (all users, no admin permission). */
  private readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  readonly progress = signal<ChecklistProgress | null>(null);
  readonly summary = signal<ReviewSummary | null>(null);
  readonly failItems = signal<ChecklistProgressItem[]>([]);
  /** itemId → response comment for fail items, used to prefill the raise dialog. */
  private readonly failComments = signal<Record<string, string>>({});

  private usersCache: UserDto[] = [];

  /** itemId → loaded response (lazy, populated when a panel expands). */
  readonly responses = signal<Record<string, ChecklistResponse | null>>({});
  /** responseId → evidence list (lazy). */
  readonly evidence = signal<Record<string, EvidenceFile[]>>({});

  private lastAuditId = '';

  /* ---- Permissions / status gating ---- */

  readonly status = computed(() => this.audit().status);
  readonly isInProgress = computed(() => this.status() === 'in_progress');
  readonly isUnderReview = computed(() => this.status() === 'under_review');
  readonly isReadOnly = computed(
    () =>
      this.status() === 'under_review' ||
      this.status() === 'completed' ||
      this.status() === 'cancelled',
  );

  private readonly canRespondPerm = computed(() =>
    this.auth.hasPermission(Permissions.RespondItem),
  );
  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageAudit),
  );
  readonly canUploadEvidence = computed(() =>
    this.auth.hasPermission(Permissions.UploadEvidence),
  );
  readonly canManageEvidence = computed(() =>
    this.auth.hasPermission(Permissions.ManageEvidence),
  );
  readonly canViewEvidence = computed(
    () =>
      this.auth.hasPermission(Permissions.ViewEvidence) ||
      this.auth.hasPermission(Permissions.ViewAudit),
  );
  readonly canRaiseException = computed(() =>
    this.auth.hasPermission(Permissions.RaiseException),
  );
  /** Show the time/effort panel to anyone who can log or view time entries. */
  readonly canViewTime = computed(
    () =>
      this.auth.hasPermission(Permissions.LogTime) ||
      this.auth.hasPermission(Permissions.ViewTimeEntries),
  );

  /** Responding / discarding needs RespondItem AND an in_progress audit. */
  readonly canRespond = computed(
    () => this.canRespondPerm() && this.isInProgress(),
  );

  /**
   * Worklist of failed items still needing an exception. Anyone with RaiseException sees it while the
   * checklist is live (in progress or under review) so they can raise directly — not just the reviewer.
   * Reviewers keep it throughout review (including the "all clear" empty state) to also record judgement.
   */
  readonly showFailWorklist = computed(() => {
    if (!this.isInProgress() && !this.isUnderReview()) {
      return false;
    }
    const canReview = this.canManage() && this.isUnderReview();
    if (!this.canRaiseException() && !canReview) {
      return false;
    }
    // Reviewers keep the panel throughout review; auditors see it only when something needs action.
    return canReview || this.failItems().length > 0;
  });

  readonly progressPct = computed(() => {
    const p = this.progress();
    if (!p || p.totalItems === 0) {
      return 0;
    }
    return Math.round((p.respondedItems / p.totalItems) * 100);
  });

  /** Active team members eligible to be assigned items. */
  readonly assignableMembers = computed<AssignableMember[]>(() =>
    (this.audit().teamMembers ?? [])
      .filter((m) => m.isActive)
      .map((m) => ({
        userId: m.userId,
        displayName: this.nameOf(m.userId),
        teamRole: m.teamRole,
      })),
  );

  /** Progress items grouped by section, ordered by orderIndex. */
  readonly progressGroups = computed<ProgressGroup[]>(() => {
    const items = [...(this.progress()?.items ?? [])].sort(
      (a, b) => a.orderIndex - b.orderIndex,
    );
    const groups: ProgressGroup[] = [];
    const byName = new Map<string, ProgressGroup>();
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

  /** Count of responded items within a checklist section (for the section header meta). */
  sectionDone(group: ProgressGroup): number {
    return group.items.filter((i) => i.itemState === 'responded').length;
  }

  /* ---- Master-detail selection ---- */

  /** The checklist item currently open in the workspace (right pane). */
  readonly selectedItemId = signal<string | null>(null);

  /** Scope for the evidence / procedures / time tabs: the open item, or the whole audit. */
  readonly panelScope = signal<'item' | 'audit'>('item');

  /** The id passed to the panels — the selected item when scoped to it, else null (whole audit). */
  readonly scopedIdForPanels = computed(() =>
    this.panelScope() === 'item' ? this.selectedItemId() : null,
  );

  /** All progress items flattened in display order (across sections). */
  readonly orderedItems = computed<ChecklistProgressItem[]>(() =>
    this.progressGroups().flatMap((g) => g.items),
  );

  /** The resolved selected item (null until progress loads / nothing selected). */
  readonly selectedItem = computed<ChecklistProgressItem | null>(
    () => this.orderedItems().find((i) => i.itemId === this.selectedItemId()) ?? null,
  );

  /** 0-based position of the selected item across the whole checklist (-1 if none). */
  readonly selectedIndex = computed(() =>
    this.orderedItems().findIndex((i) => i.itemId === this.selectedItemId()),
  );

  /** A concise dot state for the rail marker: verdict wins once responded, else the workflow state. */
  railState(item: ChecklistProgressItem): string {
    if (item.itemState === 'responded' && item.verdict) {
      return item.verdict; // pass | fail | na
    }
    return item.itemState; // not_started | in_progress | responded
  }

  /** Opens a checklist item in the workspace and lazily loads its response + evidence. */
  selectItem(item: ChecklistProgressItem): void {
    this.selectedItemId.set(item.itemId);
    this.loadResponse(item);
  }

  /** Moves to the next / previous checklist item (wrapping is intentionally disabled). */
  selectNext(): void {
    const items = this.orderedItems();
    const next = items[this.selectedIndex() + 1];
    if (next) {
      this.selectItem(next);
    }
  }
  selectPrevious(): void {
    const prev = this.orderedItems()[this.selectedIndex() - 1];
    if (prev) {
      this.selectItem(prev);
    }
  }
  readonly hasNext = computed(() => this.selectedIndex() >= 0 && this.selectedIndex() < this.orderedItems().length - 1);
  readonly hasPrevious = computed(() => this.selectedIndex() > 0);

  /** Keeps a valid selection after (re)load: first item by default, or clears if the item vanished. */
  private ensureSelection(): void {
    const items = this.orderedItems();
    if (!items.length) {
      this.selectedItemId.set(null);
      return;
    }
    const current = this.selectedItemId();
    if (!current || !items.some((i) => i.itemId === current)) {
      this.selectItem(items[0]);
    }
  }

  constructor() {
    // Refetch progress / summary whenever the audit id first appears or changes.
    effect(() => {
      const id = this.audit().id;
      if (id && id !== this.lastAuditId) {
        this.lastAuditId = id;
        this.refresh();
      }
    });
  }

  /** Reloads progress, review summary, and (when reviewing) the fail list. */
  refresh(): void {
    const id = this.audit().id;
    this.service.getChecklistProgress(id).subscribe({
      next: (p) => {
        this.progress.set(p);
        this.ensureSelection();
      },
    });
    this.service.getReviewSummary(id).subscribe({
      next: (s) => this.summary.set(s),
    });
    // Load the fail worklist for anyone who can act on it (raise an exception or, as reviewer, record
    // judgement) while the checklist is live — so auditors get the same at-a-glance shortcut as reviewers.
    if (
      (this.canRaiseException() || this.canManage()) &&
      (this.isInProgress() || this.isUnderReview())
    ) {
      this.loadFailItems();
    }
  }

  private loadFailItems(): void {
    this.service.getFailWithoutException(this.audit().id).subscribe({
      next: (r) => {
        // Map onto the richer progress items so the panel can reuse the template.
        const byId = new Map(
          (this.progress()?.items ?? []).map((i) => [i.itemId, i]),
        );
        // Capture the response comment so the raise dialog can prefill it.
        const comments: Record<string, string> = {};
        for (const f of r.items) {
          if (f.comment) {
            comments[f.itemId] = f.comment;
          }
        }
        this.failComments.set(comments);
        this.failItems.set(
          r.items.map(
            (f) =>
              byId.get(f.itemId) ?? {
                itemId: f.itemId,
                orderIndex: 0,
                prompt: f.prompt,
                itemState: 'responded',
                verdict: 'fail',
                isRequired: false,
                assignedUserId: f.assignedUserId ?? null,
                hasException: false,
                responseType: 'pass_fail_na',
              },
          ),
        );
      },
    });
  }

  nameOf(userId: string | null | undefined): string {
    return this.userLookup.displayName(userId);
  }

  private version(): string {
    return this.audit().version;
  }

  /** After a version-bearing mutation: ask parent to reload, then refresh ours. */
  private afterMutation(message: string): void {
    this.notify.success(message);
    this.reloadRequested.emit();
    this.refresh();
  }

  private handleError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 409) {
      const problem = err.error as ProblemDetails | null;
      if (problem?.error_code === CONCURRENCY_CONFLICT) {
        // Interceptor already toasted; reload to pick up the fresh version.
        this.reloadRequested.emit();
        this.refresh();
      }
    }
  }

  /* ---- Responses ---- */

  respond(item: ChecklistProgressItem): void {
    this.service.getResponse(this.audit().id, item.itemId).subscribe({
      next: (current) => this.openRespondDialog(item, current),
      error: () => this.openRespondDialog(item, null),
    });
  }

  private openRespondDialog(
    item: ChecklistProgressItem,
    current: ChecklistResponse | null,
  ): void {
    const data: RespondItemDialogData = {
      prompt: item.prompt,
      responseType: item.responseType,
      responseConfigJson: item.responseConfigJson,
      current,
    };
    this.dialog
      .open(RespondItemDialogComponent, { data, width: '520px' })
      .afterClosed()
      .subscribe((result?: RespondItemDialogResult) => {
        if (!result) {
          return;
        }
        this.service
          .submitResponse(this.audit().id, item.itemId, {
            verdict: result.verdict,
            comment: result.comment,
            valueJson: result.valueJson,
            isDraft: result.isDraft,
            version: this.version(),
          })
          .subscribe({
            next: (saved) => {
              this.patchResponse(item.itemId, saved);
              this.loadEvidence(saved.id);
              this.afterMutation(
                result.isDraft ? 'Draft saved.' : 'Response submitted.',
              );
            },
            error: (err: unknown) => this.handleError(err),
          });
      });
  }

  discardDraft(item: ChecklistProgressItem): void {
    this.service
      .discardDraft(this.audit().id, item.itemId, this.version())
      .subscribe({
        next: () => {
          this.patchResponse(item.itemId, null);
          this.afterMutation('Draft discarded.');
        },
        error: (err: unknown) => this.handleError(err),
      });
  }

  /** Lazily loads the response (and its evidence) when an item panel opens. */
  loadResponse(item: ChecklistProgressItem): void {
    if (item.itemId in this.responses()) {
      this.loadEvidence(this.responseFor(item.itemId)?.id);
      return;
    }
    this.service.getResponse(this.audit().id, item.itemId).subscribe({
      next: (r) => {
        this.patchResponse(item.itemId, r);
        this.loadEvidence(r?.id);
      },
    });
  }

  responseFor(itemId: string): ChecklistResponse | null {
    return this.responses()[itemId] ?? null;
  }

  hasDraft(itemId: string): boolean {
    return this.responseFor(itemId)?.isDraft === true;
  }

  private patchResponse(itemId: string, value: ChecklistResponse | null): void {
    this.responses.update((m) => ({ ...m, [itemId]: value }));
  }

  showHistory(item: ChecklistProgressItem): void {
    const data: ResponseHistoryDialogData = {
      auditId: this.audit().id,
      itemId: item.itemId,
      prompt: item.prompt,
    };
    this.dialog.open(ResponseHistoryDialogComponent, { data, width: '520px' });
  }

  /* ---- Assignment ---- */

  assign(item: ChecklistProgressItem): void {
    const data: AssignItemDialogData = {
      prompt: item.prompt,
      currentUserId: item.assignedUserId ?? null,
      members: this.assignableMembers(),
    };
    this.dialog
      .open(AssignItemDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: AssignItemDialogResult) => {
        if (!result) {
          return;
        }
        this.runAuditMutation(
          this.service.assignItem(this.audit().id, item.itemId, {
            assignedUserId: result.assignedUserId,
            version: this.version(),
          }),
          'Assignment updated.',
        );
      });
  }

  /* ---- Review judgement ---- */

  recordJudgement(item: ChecklistProgressItem): void {
    const data: FailJudgementDialogData = { prompt: item.prompt };
    this.dialog
      .open(FailJudgementDialogComponent, { data, width: '520px' })
      .afterClosed()
      .subscribe((result?: FailJudgementDialogResult) => {
        if (!result) {
          return;
        }
        this.runAuditMutation(
          this.service.recordFailJudgement(this.audit().id, item.itemId, {
            justification: result.justification,
            version: this.version(),
          }),
          'Judgement recorded.',
        );
      });
  }

  /** Shared handler for endpoints that return the fresh audit. */
  private runAuditMutation(op: Observable<Audit>, message: string): void {
    op.subscribe({
      next: () => this.afterMutation(message),
      error: (err: unknown) => this.handleError(err),
    });
  }

  /* ---- Raise exception from a failed item ---- */

  raiseException(item: ChecklistProgressItem): void {
    if (this.usersCache.length) {
      this.openRaiseDialog(item, this.usersCache);
      return;
    }
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        this.openRaiseDialog(item, page.items);
      },
      error: () => this.openRaiseDialog(item, []),
    });
  }

  private openRaiseDialog(
    item: ChecklistProgressItem,
    users: UserDto[],
  ): void {
    const comment = this.failComments()[item.itemId] ?? '';
    const data: RaiseExceptionDialogData = {
      checklistItemId: item.itemId,
      title: item.prompt,
      rootCause: comment,
      recommendation: comment,
      users,
    };
    this.dialog
      .open(RaiseExceptionDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: RaiseExceptionRequest) => {
        if (!result) {
          return;
        }
        this.exceptions.raise(this.audit().id, result).subscribe({
          next: () => {
            this.notify.success('Exception raised.');
            // The item now has an exception; refresh progress + the fail list.
            this.refresh();
          },
        });
      });
  }

  /* ---- Evidence (NO audit version) ---- */

  loadEvidence(responseId: string | null | undefined): void {
    if (!responseId || responseId in this.evidence()) {
      return;
    }
    this.service.listEvidence(this.audit().id, responseId).subscribe({
      next: (files) => this.patchEvidence(responseId, files),
    });
  }

  evidenceFor(responseId: string | null | undefined): EvidenceFile[] {
    if (!responseId) {
      return [];
    }
    return this.evidence()[responseId] ?? [];
  }

  uploadEvidence(responseId: string, input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.service.uploadEvidence(this.audit().id, responseId, file).subscribe({
      next: (saved) => {
        this.patchEvidence(responseId, [
          ...this.evidenceFor(responseId),
          saved,
        ]);
        this.notify.success('Evidence uploaded.');
        input.value = '';
      },
      error: () => {
        input.value = '';
      },
    });
  }

  downloadEvidence(file: EvidenceFile): void {
    this.service.downloadEvidence(this.audit().id, file.id).subscribe({
      next: (blob) => this.triggerDownload(blob, file.originalFilename),
    });
  }

  deleteEvidence(responseId: string, file: EvidenceFile): void {
    const data: TransitionReasonDialogData = {
      title: 'Delete evidence',
      message: `Delete "${file.originalFilename}"? Provide a reason.`,
      reasonRequired: true,
      confirmLabel: 'Delete',
      destructive: true,
    };
    this.dialog
      .open(TransitionReasonDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: TransitionReasonResult) => {
        if (!result) {
          return;
        }
        this.service
          .deleteEvidence(this.audit().id, file.id, result.reason)
          .subscribe({
            next: () => {
              this.patchEvidence(
                responseId,
                this.evidenceFor(responseId).filter((e) => e.id !== file.id),
              );
              this.notify.success('Evidence deleted.');
            },
          });
      });
  }

  private patchEvidence(responseId: string, files: EvidenceFile[]): void {
    this.evidence.update((m) => ({ ...m, [responseId]: files }));
  }

  private triggerDownload(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  /* ---- Display helpers ---- */

  verdictLabel(verdict: ResponseVerdict | null | undefined): string {
    switch (verdict) {
      case 'pass':
        return 'Pass';
      case 'fail':
        return 'Fail';
      case 'na':
        return 'N/A';
      default:
        return 'Draft';
    }
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  /**
   * The captured answer for a value-type item (text / numeric / date / rating / choice), formatted
   * for display — pass/fail/n-a items have no value (they use the verdict chip instead).
   */
  answerText(itemId: string): string | null {
    const json = this.responseFor(itemId)?.valueJson;
    if (!json) {
      return null;
    }
    try {
      const v = JSON.parse(json) as Record<string, unknown>;
      const raw = v['text'] ?? v['number'] ?? v['date'] ?? v['rating'] ?? v['choice'];
      return raw === undefined || raw === null ? null : String(raw);
    } catch {
      return null;
    }
  }
}
