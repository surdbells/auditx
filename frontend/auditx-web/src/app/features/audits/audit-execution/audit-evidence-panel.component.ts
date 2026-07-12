import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';

import { EvidenceRequestsService } from '../../../core/services/evidence-requests.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { Permissions } from '../../../core/permissions';
import { Audit, EvidenceRequest } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import {
  TransitionReasonDialogComponent,
  TransitionReasonDialogData,
  TransitionReasonResult,
} from '../dialogs/transition-reason-dialog.component';

/** Audit statuses in which evidence may be requested / actioned (mirrors the backend Actionable set). */
const ACTIONABLE = new Set(['planned', 'in_progress', 'under_review']);

/**
 * Expected/requested-evidence panel for the audit-execution screen (P2-D). Self-manages its own request list;
 * evidence requests are a separate aggregate so they do NOT change the audit version.
 */
@Component({
  selector: 'app-audit-evidence-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    IconComponent,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  templateUrl: './audit-evidence-panel.component.html',
  styleUrl: './audit-evidence-panel.component.scss',
})
export class AuditEvidencePanelComponent {
  readonly audit = input.required<Audit>();
  /** When set, the panel is scoped to one checklist item (filter + pre-attach + hide the item picker). */
  readonly scopedItemId = input<string | null>(null);

  readonly isScoped = computed(() => !!this.scopedItemId());

  /** Requests shown — filtered to the scoped item when scoped, else all. */
  readonly visibleRequests = computed(() => {
    const scope = this.scopedItemId();
    return scope ? this.requests().filter((r) => r.checklistItemId === scope) : this.requests();
  });

  private readonly service = inject(EvidenceRequestsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);
  private readonly lookup = inject(UserLookupService);
  private readonly refLookup = inject(ReferenceDataLookupService);

  /** Active evidence document-type reference-data items (lazy-loaded). */
  readonly documentTypes = this.refLookup.options('evidence_document_type');

  readonly requests = signal<EvidenceRequest[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly adding = signal(false);
  private lastAuditId = '';

  // Every evidence mutation is gated server-side by [RequirePermission(RespondItem)], so the client gate is
  // RespondItem alone — ManageAudit is only a resource-scope alternative to team membership, not a substitute
  // for the permission (mirrors the sibling procedures panel's canRecord).
  readonly canManage = computed(() => this.auth.hasPermission(Permissions.RespondItem));
  readonly canActionNow = computed(() => this.canManage() && ACTIONABLE.has(this.audit().status));

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    documentType: [''],
    checklistItemId: [''],
    dueDate: [''],
    notes: [''],
  });

  constructor() {
    // The user directory + document types load lazily on first use (mirrors the procedures panel).
    effect(() => {
      const id = this.audit().id;
      if (id && id !== this.lastAuditId) {
        this.lastAuditId = id;
        this.refresh();
      }
    });
  }

  refresh(): void {
    this.loading.set(true);
    this.service.list(this.audit().id).subscribe({
      next: (rows) => {
        this.requests.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  nameOf(userId: string | null | undefined): string {
    return this.lookup.displayName(userId);
  }

  documentTypeLabel(code: string | null): string | null {
    return code ? this.refLookup.label('evidence_document_type', code) : null;
  }

  promptFor(checklistItemId: string | null): string | null {
    if (!checklistItemId) {
      return null;
    }
    return this.audit().checklistItems.find((i) => i.id === checklistItemId)?.prompt ?? null;
  }

  startAdd(): void {
    this.adding.set(true);
    this.form.reset({ title: '', documentType: '', checklistItemId: this.scopedItemId() ?? '', dueDate: '', notes: '' });
  }

  cancelAdd(): void {
    this.adding.set(false);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.submitting.set(true);
    this.service
      .request(this.audit().id, {
        title: v.title.trim(),
        documentType: v.documentType || null,
        checklistItemId: v.checklistItemId || null,
        dueDate: v.dueDate || null,
        notes: v.notes.trim() || null,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.notify.success(this.i18n.translate('evidence.notify.requested'));
          this.adding.set(false);
          this.refresh();
        },
        error: () => this.submitting.set(false),
      });
  }

  markReceived(r: EvidenceRequest): void {
    this.service.markReceived(r.id, r.version).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('evidence.notify.received'));
        this.refresh();
      },
    });
  }

  waive(r: EvidenceRequest): void {
    const data: TransitionReasonDialogData = {
      title: this.i18n.translate('evidence.waive.title'),
      message: this.i18n.translate('evidence.waive.message', { title: r.title }),
      reasonRequired: true,
      confirmLabel: this.i18n.translate('evidence.actions.waive'),
    };
    this.dialog
      .open(TransitionReasonDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: TransitionReasonResult) => {
        if (!result) {
          return;
        }
        this.service.waive(r.id, result.reason, r.version).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('evidence.notify.waived'));
            this.refresh();
          },
        });
      });
  }

  remove(r: EvidenceRequest): void {
    this.service.delete(r.id, r.version).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('evidence.notify.deleted'));
        this.refresh();
      },
    });
  }
}
