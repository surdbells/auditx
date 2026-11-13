import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';

import { IconComponent } from '../../core/icons/icon.component';
import { EvidenceRequestsService } from '../../core/services/evidence-requests.service';
import { AuditsService } from '../../core/services/audits.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { EvidenceFile, EvidenceRequest } from '../../core/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';
import {
  TransitionReasonDialogComponent,
  TransitionReasonDialogData,
  TransitionReasonResult,
} from '../audits/dialogs/transition-reason-dialog.component';

/**
 * The auditee's personal document-request worklist (reached from the request email). Lists the documents the
 * auditor has asked for, and lets the auditee upload each one — which marks the request received and surfaces the
 * file on the auditor's screen. A document sent by mistake can be RECALLED: it is taken back from the auditors but
 * stays on the record (marked recalled, with the reason), so the history of what was provided is never lost.
 */
@Component({
  selector: 'app-my-evidence-requests',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatTooltipModule,
    IconComponent,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    TranslatePipe,
  ],
  template: `
    <app-page-header [title]="'myEvidence.title' | t" [subtitle]="'myEvidence.subtitle' | t" />

    @if (loading()) {
      <app-loading [message]="'myEvidence.loading' | t" />
    } @else if (!requests().length) {
      <app-empty-state icon="task_alt" [title]="'myEvidence.empty.title' | t" [message]="'myEvidence.empty.message' | t" />
    } @else {
      @for (r of requests(); track r.id) {
        <mat-card appearance="outlined" class="req">
          <div class="req__head">
            <div>
              <h3 class="req__title">{{ r.title }}</h3>
              <p class="muted req__meta">
                {{ 'evidence.purpose.' + r.purpose | t }} ·
                {{ 'myEvidence.due' | t }}: {{ r.dueDate ?? '—' }}
                @if (r.isOverdue) { · <span class="req__overdue">{{ 'myEvidence.overdue' | t }}</span> }
              </p>
            </div>
            <span class="status-badge" [attr.data-status]="r.status === 'received' ? 'responded' : (r.status === 'waived' ? 'muted' : 'open')">
              {{ 'evidence.status.' + r.status | t }}
            </span>
          </div>

          @if (r.notes) { <p class="req__notes">{{ r.notes }}</p> }

          @if (r.files.length) {
            <ul class="req__files">
              @for (f of r.files; track f.id) {
                @if (f.isRecalled) {
                  <!-- Taken back: kept on the record as history, no longer a live document. -->
                  <li class="req__file req__file--recalled">
                    <app-icon inline name="block" />
                    <span class="req__file-name">{{ f.originalFilename }}</span>
                    <span class="status-badge" data-status="cancelled">{{ 'evidence.recalled.badge' | t }}</span>
                    <span class="muted req__file-note">
                      {{ 'myEvidence.recalledOn' | t: { date: (f.recalledAt | date: 'medium') ?? '' } }}
                      @if (f.recallReason) { — {{ f.recallReason }} }
                    </span>
                  </li>
                } @else {
                  <li class="req__file">
                    <app-icon inline name="description" />
                    <button matButton type="button" (click)="download(r, f)">{{ f.originalFilename }}</button>
                    <span class="muted req__file-note">{{ 'myEvidence.uploadedOn' | t: { date: (f.uploadedAt | date: 'medium') ?? '' } }}</span>
                    @if (canRecall(f)) {
                      <button
                        matButton
                        type="button"
                        class="req__recall"
                        [disabled]="recallingId() === f.id"
                        [matTooltip]="'myEvidence.recallHint' | t"
                        [attr.aria-label]="'myEvidence.recallAria' | t: { name: f.originalFilename }"
                        (click)="recall(r, f)"
                      >
                        <app-icon name="undo" />
                        {{ 'myEvidence.recall' | t }}
                      </button>
                    }
                  </li>
                }
              }
            </ul>
          }

          @if (r.status !== 'waived') {
            <div class="req__actions">
              <input #fileInput type="file" hidden (change)="onFile(r, fileInput)" />
              <button matButton="filled" type="button" [disabled]="uploadingId() === r.id" (click)="fileInput.click()">
                <app-icon name="upload_file" />
                {{ (hasLiveFile(r) ? 'myEvidence.uploadAnother' : 'myEvidence.upload') | t }}
              </button>
            </div>
          }
        </mat-card>
      }
    }
  `,
  styles: `
    .req { margin-bottom: 1rem; padding: 1rem 1.25rem; }
    .req__head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .req__title { margin: 0; font-size: 1.02rem; }
    .req__meta { margin: 0.2rem 0 0; font: var(--mat-sys-label-medium); }
    .req__overdue { color: var(--mat-sys-error); font-weight: 600; }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .req__notes { margin: 0.6rem 0 0; }
    .req__files { list-style: none; margin: 0.6rem 0 0; padding: 0; display: flex; flex-direction: column; gap: 0.2rem; }
    .req__file { display: flex; flex-wrap: wrap; align-items: center; gap: 0.3rem 0.5rem; }
    .req__file-note { font-size: 0.8rem; }
    .req__recall { margin-left: auto; --mat-text-button-label-text-color: var(--mat-sys-error); }
    /* A recalled document reads as history: struck through, with why and when beside it. */
    .req__file--recalled { padding: 0.35rem 0; }
    .req__file--recalled .req__file-name { text-decoration: line-through; color: var(--mat-sys-on-surface-variant); }
    .req__actions { margin-top: 0.75rem; }
  `,
})
export class MyEvidenceRequestsComponent {
  private readonly service = inject(EvidenceRequestsService);
  private readonly audits = inject(AuditsService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);

  readonly requests = signal<EvidenceRequest[]>([]);
  readonly loading = signal(true);
  readonly uploadingId = signal<string | null>(null);
  /** The document whose recall is in flight (disables its button). */
  readonly recallingId = signal<string | null>(null);

  private readonly currentUserId = computed(() => this.auth.session()?.userId ?? null);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.loading.set(true);
    this.service.mine(false).subscribe({
      next: (rows) => {
        this.requests.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /** You can take back only what you uploaded yourself, and only while it has not already been recalled. */
  canRecall(f: EvidenceFile): boolean {
    return !f.isRecalled && !!this.currentUserId() && f.uploadedBy === this.currentUserId();
  }

  /** True when at least one document on the request is still live (not recalled). */
  hasLiveFile(r: EvidenceRequest): boolean {
    return r.files.some((f) => !f.isRecalled);
  }

  onFile(r: EvidenceRequest, input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    this.uploadingId.set(r.id);
    this.service.uploadFile(r.id, file).subscribe({
      next: (updated) => {
        this.uploadingId.set(null);
        this.requests.update((all) => all.map((x) => (x.id === updated.id ? updated : x)));
        this.notify.success(this.i18n.translate('myEvidence.notify.uploaded'));
      },
      error: () => this.uploadingId.set(null),
    });
  }

  /**
   * Recall a document: ask why (mandatory — the reason is recorded and shown to the auditor), then take it back.
   * The document stays listed as recalled; if it was the only one, the request becomes outstanding again.
   */
  recall(r: EvidenceRequest, f: EvidenceFile): void {
    const data: TransitionReasonDialogData = {
      title: this.i18n.translate('myEvidence.recallDialog.title'),
      message: this.i18n.translate('myEvidence.recallDialog.message', { name: f.originalFilename }),
      confirmLabel: this.i18n.translate('myEvidence.recall'),
      reasonRequired: true,
      destructive: true,
    };
    this.dialog
      .open(TransitionReasonDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: TransitionReasonResult) => {
        const reason = result?.reason?.trim();
        if (!reason) {
          return;
        }
        this.recallingId.set(f.id);
        this.service.recallFile(r.id, f.id, reason).subscribe({
          next: (updated) => {
            this.recallingId.set(null);
            this.requests.update((all) => all.map((x) => (x.id === updated.id ? updated : x)));
            this.notify.success(
              this.i18n.translate(updated.status === 'requested' ? 'myEvidence.notify.recalledReopened' : 'myEvidence.notify.recalled'),
            );
          },
          error: () => this.recallingId.set(null),
        });
      });
  }

  download(r: EvidenceRequest, f: EvidenceFile): void {
    this.audits.downloadEvidence(r.auditId, f.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = f.originalFilename;
        a.click();
        URL.revokeObjectURL(url);
      },
    });
  }
}
