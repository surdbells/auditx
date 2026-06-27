import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';

import { AuditTrailService } from '../../../core/services/audit-trail.service';
import { NotificationService } from '../../../core/services/notification.service';
import { FlaggedEvidence } from '../../../core/models';
import {
  UnflagEvidenceDialogComponent,
  UnflagEvidenceDialogData,
  UnflagEvidenceDialogResult,
} from './dialogs/unflag-evidence-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-evidence-integrity',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './evidence-integrity.component.html',
  styleUrl: './evidence-integrity.component.scss',
})
export class EvidenceIntegrityComponent {
  private readonly service = inject(AuditTrailService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);

  readonly displayedColumns = [
    'filename',
    'auditId',
    'sha256',
    'uploadedAt',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly flagged = signal<FlaggedEvidence[]>([]);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.flagged().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.listFlagged().subscribe({
      next: (items) => {
        this.flagged.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  unflag(evidence: FlaggedEvidence): void {
    const data: UnflagEvidenceDialogData = { evidence };
    this.dialog
      .open(UnflagEvidenceDialogComponent, { data, width: '520px' })
      .afterClosed()
      .subscribe((result?: UnflagEvidenceDialogResult) => {
        if (!result) {
          return;
        }
        this.service
          .unflagEvidence(evidence.auditId, evidence.id, result.resolution)
          .subscribe({
            next: () => {
              this.notify.success('Evidence unflagged.');
              this.fetch();
            },
          });
      });
  }
}
