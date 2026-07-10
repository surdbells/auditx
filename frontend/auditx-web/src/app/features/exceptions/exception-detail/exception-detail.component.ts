import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { Observable } from 'rxjs';

import { ExceptionsService } from '../../../core/services/exceptions.service';
import { UsersService } from '../../../core/services/users.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  EvidenceFile,
  Exception,
  FindingControlLink,
  FindingLinks,
  FindingRegulationLink,
  MapAction,
  ProblemDetails,
  UserDto,
} from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import {
  SubmitMapDialogComponent,
  SubmitMapDialogData,
  SubmitMapDialogResult,
} from '../dialogs/submit-map-dialog.component';
import {
  ChangeSeverityDialogComponent,
  ChangeSeverityDialogData,
  ChangeSeverityDialogResult,
} from '../dialogs/change-severity-dialog.component';
import {
  ReassignOwnerDialogComponent,
  ReassignOwnerDialogData,
  ReassignOwnerDialogResult,
} from '../dialogs/reassign-owner-dialog.component';
import {
  ExceptionReasonDialogComponent,
  ExceptionReasonDialogData,
  ExceptionReasonResult,
} from '../dialogs/exception-reason-dialog.component';
import {
  ExceptionHistoryDialogComponent,
  ExceptionHistoryDialogData,
} from '../dialogs/exception-history-dialog.component';
import {
  LinkFindingDialogComponent,
  LinkFindingDialogData,
  LinkFindingKind,
  LinkFindingResult,
} from '../dialogs/link-finding-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

const CONCURRENCY_CONFLICT = 'exception.concurrency_conflict';

@Component({
  selector: 'app-exception-detail',
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
    TranslatePipe,
  ],
  templateUrl: './exception-detail.component.html',
  styleUrl: './exception-detail.component.scss',
})
export class ExceptionDetailComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(ExceptionsService);
  private readonly users = inject(UsersService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly state = signal<ViewState>('loading');
  readonly exception = signal<Exception | null>(null);
  readonly userNames = signal<Record<string, string>>({});
  /** actionId → evidence list (lazy). */
  readonly evidence = signal<Record<string, EvidenceFile[]>>({});
  /** Linked controls + regulations (P1-B). */
  readonly links = signal<FindingLinks | null>(null);

  private usersCache: UserDto[] = [];

  /* ---- Permissions ---- */

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageException),
  );
  readonly canSubmitMap = computed(() =>
    this.auth.hasPermission(Permissions.SubmitMap),
  );
  readonly canApproveMap = computed(() =>
    this.auth.hasPermission(Permissions.ApproveMap),
  );
  readonly canClose = computed(() =>
    this.auth.hasPermission(Permissions.CloseException),
  );
  readonly canCancel = computed(() =>
    this.auth.hasPermission(Permissions.CancelException),
  );
  readonly canCia = computed(() => this.auth.hasPermission(Permissions.CIA));
  readonly canUploadEvidence = computed(() =>
    this.auth.hasPermission(Permissions.UploadEvidence),
  );
  readonly canViewEvidence = computed(
    () =>
      this.auth.hasPermission(Permissions.ViewEvidence) ||
      this.auth.hasPermission(Permissions.ViewExceptions),
  );

  /* ---- Status gating ---- */

  readonly status = computed(() => this.exception()?.status ?? null);
  readonly isReadOnly = computed(
    () => this.status() === 'closed' || this.status() === 'cancelled',
  );

  readonly canSubmitMapNow = computed(
    () =>
      this.canSubmitMap() &&
      (this.status() === 'open' || this.status() === 'map_rejected'),
  );
  readonly canReviewMap = computed(
    () => this.canApproveMap() && this.status() === 'map_submitted',
  );
  readonly canMarkMapComplete = computed(
    () =>
      this.canSubmitMap() &&
      this.status() === 'map_approved' &&
      this.allActionsComplete(),
  );
  readonly canReturnForEvidence = computed(
    () => this.canApproveMap() && this.status() === 'pending_closure',
  );
  readonly canCloseNow = computed(
    () => this.canClose() && this.status() === 'pending_closure',
  );
  readonly canCiaCountersign = computed(
    () => this.canCia() && this.exception()?.ciaPending === true,
  );
  readonly canManageNow = computed(() => this.canManage() && !this.isReadOnly());
  readonly canCancelNow = computed(() => this.canCancel() && !this.isReadOnly());
  /** MAP actions may be worked while approved (before mark-complete). */
  readonly canWorkActions = computed(
    () => this.canSubmitMap() && this.status() === 'map_approved',
  );

  readonly ownerName = computed(() =>
    this.nameOf(this.exception()?.ownerUserId),
  );
  readonly raisedByName = computed(() =>
    this.nameOf(this.exception()?.raisedByUserId),
  );

  allActionsComplete(): boolean {
    const actions = this.exception()?.mapActions ?? [];
    return actions.length > 0 && actions.every((a) => a.status === 'complete');
  }

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getById(this.id()).subscribe({
      next: (ex) => {
        this.exception.set(ex);
        this.state.set('ready');
        this.ensureUsers();
        this.loadLinks();
      },
      error: () => this.state.set('error'),
    });
  }

  private loadLinks(): void {
    this.service.getLinks(this.id()).subscribe({
      next: (links) => this.links.set(links),
      // Non-fatal: the links panel just stays empty if it can't load.
      error: () => this.links.set({ controls: [], regulations: [] }),
    });
  }

  reload(): void {
    this.service.getById(this.id()).subscribe({
      next: (ex) => {
        this.exception.set(ex);
        this.ensureUsers();
      },
    });
  }

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
        // Non-fatal: ids display verbatim.
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

  private version(): string {
    return this.exception()!.version;
  }

  /** Applies a fresh exception returned by a mutation and toasts. */
  private runMutation(op: Observable<Exception>, message: string): void {
    op.subscribe({
      next: (ex) => {
        this.exception.set(ex);
        this.ensureUsers();
        this.notify.success(message);
      },
      error: (err: unknown) => this.handleError(err),
    });
  }

  private handleError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 409) {
      const problem = err.error as ProblemDetails | null;
      if (problem?.error_code === CONCURRENCY_CONFLICT) {
        // Interceptor already toasted; reload for a fresh version.
        this.reload();
      }
    }
  }

  /* ---- MAP ---- */

  submitMap(): void {
    const data: SubmitMapDialogData = { users: this.usersCache };
    this.dialog
      .open(SubmitMapDialogComponent, { data, width: '640px' })
      .afterClosed()
      .subscribe((result?: SubmitMapDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.submitMap(this.id(), {
            actions: result.actions,
            version: this.version(),
          }),
          this.i18n.translate('exceptions.notify.mapSubmitted'),
        );
      });
  }

  approveMap(): void {
    this.service
      .approveMap(this.id(), { version: this.version() })
      .subscribe({
        next: (res) => {
          if (res.exception) {
            this.exception.set(res.exception);
            this.ensureUsers();
            this.notify.success(this.i18n.translate('exceptions.notify.mapApproved'));
          } else {
            // 202 maker-checker-gated: status stays map_submitted; reload.
            this.notify.info(this.i18n.translate('exceptions.notify.approvalSubmitted'));
            this.reload();
          }
        },
        error: (err: unknown) => this.handleError(err),
      });
  }

  rejectMap(): void {
    this.openReason(
      {
        title: this.i18n.translate('exceptions.dialog.rejectMapTitle'),
        message: this.i18n.translate('exceptions.dialog.rejectMapMessage'),
        confirmLabel: this.i18n.translate('exceptions.action.reject'),
        destructive: true,
      },
      (reason) =>
        this.runMutation(
          this.service.rejectMap(this.id(), { reason, version: this.version() }),
          this.i18n.translate('exceptions.notify.mapRejected'),
        ),
    );
  }

  markMapComplete(): void {
    this.runMutation(
      this.service.markMapComplete(this.id(), { version: this.version() }),
      this.i18n.translate('exceptions.notify.mapMarkedComplete'),
    );
  }

  returnForEvidence(): void {
    this.openReason(
      {
        title: this.i18n.translate('exceptions.action.returnForEvidence'),
        message: this.i18n.translate('exceptions.dialog.returnMessage'),
        confirmLabel: this.i18n.translate('exceptions.action.return'),
      },
      (reason) =>
        this.runMutation(
          this.service.returnForEvidence(this.id(), {
            reason,
            version: this.version(),
          }),
          this.i18n.translate('exceptions.notify.returnedForEvidence'),
        ),
    );
  }

  markActionComplete(action: MapAction): void {
    this.runMutation(
      this.service.markActionComplete(this.id(), action.id, {
        version: this.version(),
      }),
      this.i18n.translate('exceptions.notify.actionMarkedComplete'),
    );
  }

  /* ---- Management ---- */

  changeSeverity(): void {
    const ex = this.exception();
    if (!ex) {
      return;
    }
    const data: ChangeSeverityDialogData = { current: ex.severity };
    this.dialog
      .open(ChangeSeverityDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: ChangeSeverityDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.changeSeverity(this.id(), {
            severity: result.severity,
            reason: result.reason,
            version: this.version(),
          }),
          this.i18n.translate('exceptions.notify.severityUpdated'),
        );
      });
  }

  reassignOwner(): void {
    const ex = this.exception();
    if (!ex) {
      return;
    }
    const data: ReassignOwnerDialogData = {
      currentOwnerId: ex.ownerUserId,
      users: this.usersCache,
    };
    this.dialog
      .open(ReassignOwnerDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: ReassignOwnerDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.reassignOwner(this.id(), {
            ownerUserId: result.ownerUserId,
            version: this.version(),
          }),
          this.i18n.translate('exceptions.notify.ownerReassigned'),
        );
      });
  }

  cancel(): void {
    this.openReason(
      {
        title: this.i18n.translate('exceptions.dialog.cancelTitle'),
        message: this.i18n.translate('exceptions.dialog.cancelMessage'),
        confirmLabel: this.i18n.translate('exceptions.dialog.cancelTitle'),
        destructive: true,
      },
      (reason) =>
        this.runMutation(
          this.service.cancel(this.id(), { reason, version: this.version() }),
          this.i18n.translate('exceptions.notify.cancelled'),
        ),
    );
  }

  /* ---- Closure / CIA ---- */

  close(): void {
    this.openReason(
      {
        title: this.i18n.translate('exceptions.action.closeException'),
        label: this.i18n.translate('exceptions.detail.closureNote'),
        message: this.i18n.translate('exceptions.dialog.closeMessage'),
        reasonRequired: false,
        confirmLabel: this.i18n.translate('exceptions.action.closeConfirm'),
      },
      (reason) =>
        this.runMutation(
          this.service.close(this.id(), {
            closureNote: reason || null,
            version: this.version(),
          }),
          this.i18n.translate('exceptions.notify.closed'),
        ),
    );
  }

  ciaCountersign(): void {
    this.runMutation(
      this.service.ciaCountersign(this.id(), { version: this.version() }),
      this.i18n.translate('exceptions.notify.countersigned'),
    );
  }

  showHistory(): void {
    const ex = this.exception();
    if (!ex) {
      return;
    }
    const data: ExceptionHistoryDialogData = {
      exceptionId: ex.id,
      title: ex.title,
      userNames: this.userNames(),
    };
    this.dialog.open(ExceptionHistoryDialogComponent, { data, width: '520px' });
  }

  /* ---- Control / regulation links (P1-B) ---- */

  addControlLink(): void {
    this.openLinkPicker('control', (id) =>
      this.service.linkControl(this.id(), id).subscribe({
        next: () => {
          this.notify.success(this.i18n.translate('links.notify.controlLinked'));
          this.loadLinks();
        },
      }),
    );
  }

  addRegulationLink(): void {
    this.openLinkPicker('regulation', (id) =>
      this.service.linkRegulation(this.id(), id).subscribe({
        next: () => {
          this.notify.success(this.i18n.translate('links.notify.regulationLinked'));
          this.loadLinks();
        },
      }),
    );
  }

  removeControlLink(link: FindingControlLink): void {
    this.service.unlinkControl(this.id(), link.controlId).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('links.notify.controlUnlinked'));
        this.loadLinks();
      },
    });
  }

  removeRegulationLink(link: FindingRegulationLink): void {
    this.service.unlinkRegulation(this.id(), link.regulationId).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('links.notify.regulationUnlinked'));
        this.loadLinks();
      },
    });
  }

  private openLinkPicker(kind: LinkFindingKind, onResult: (id: string) => void): void {
    const current = this.links();
    const existingIds =
      kind === 'control'
        ? (current?.controls ?? []).map((c) => c.controlId)
        : (current?.regulations ?? []).map((r) => r.regulationId);
    const data: LinkFindingDialogData = { kind, existingIds };
    this.dialog
      .open(LinkFindingDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: LinkFindingResult) => {
        if (result) {
          onResult(result.id);
        }
      });
  }

  private openReason(
    config: ExceptionReasonDialogData,
    onResult: (reason: string) => void,
  ): void {
    this.dialog
      .open(ExceptionReasonDialogComponent, { data: config, width: '480px' })
      .afterClosed()
      .subscribe((result?: ExceptionReasonResult) => {
        if (!result) {
          return;
        }
        onResult(result.reason);
      });
  }

  /* ---- Evidence (NO version) ---- */

  loadEvidence(action: MapAction): void {
    if (action.id in this.evidence()) {
      return;
    }
    this.service.listActionEvidence(this.id(), action.id).subscribe({
      next: (files) => this.patchEvidence(action.id, files),
    });
  }

  evidenceFor(actionId: string): EvidenceFile[] {
    return this.evidence()[actionId] ?? [];
  }

  uploadEvidence(action: MapAction, input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.service
      .uploadActionEvidence(this.id(), action.id, file)
      .subscribe({
        next: (saved) => {
          this.patchEvidence(action.id, [
            ...this.evidenceFor(action.id),
            saved,
          ]);
          this.notify.success(this.i18n.translate('exceptions.notify.evidenceUploaded'));
          input.value = '';
        },
        error: () => {
          input.value = '';
        },
      });
  }

  downloadEvidence(file: EvidenceFile): void {
    const auditId = this.exception()?.auditId;
    if (!auditId) {
      return;
    }
    this.service.downloadEvidence(auditId, file.id).subscribe({
      next: (blob) => this.triggerDownload(blob, file.originalFilename),
    });
  }

  private patchEvidence(actionId: string, files: EvidenceFile[]): void {
    this.evidence.update((m) => ({ ...m, [actionId]: files }));
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

  formatSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
