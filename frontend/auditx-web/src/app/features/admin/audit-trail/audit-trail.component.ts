import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';

import { AuditTrailService } from '../../../core/services/audit-trail.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { AuditTrailEntry, AuditTrailFilter } from '../../../core/models';
import { humaniseActorType, humaniseEventType } from './humanise';
import {
  AuditTrailDetailDialogComponent,
  AuditTrailDetailDialogData,
} from './dialogs/audit-trail-detail-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 50;

/** Converts a Date to the start-of-day ISO datetime string. */
function toIsoStart(value: Date | null): string | undefined {
  if (!value) {
    return undefined;
  }
  const d = new Date(value);
  d.setHours(0, 0, 0, 0);
  return d.toISOString();
}

/** Converts a Date to the end-of-day ISO datetime string. */
function toIsoEnd(value: Date | null): string | undefined {
  if (!value) {
    return undefined;
  }
  const d = new Date(value);
  d.setHours(23, 59, 59, 999);
  return d.toISOString();
}

@Component({
  selector: 'app-audit-trail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './audit-trail.component.html',
  styleUrl: './audit-trail.component.scss',
})
export class AuditTrailComponent {
  private readonly service = inject(AuditTrailService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);

  readonly displayedColumns = [
    'occurredAt',
    'eventType',
    'actor',
    'targetType',
    'targetId',
  ];

  readonly filters = this.fb.nonNullable.group({
    actorUserId: '',
    eventType: '',
    targetObjectType: '',
    targetObjectId: '',
    dateFrom: null as Date | null,
    dateTo: null as Date | null,
  });

  readonly state = signal<ViewState>('loading');
  readonly entries = signal<AuditTrailEntry[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);
  readonly exporting = signal(false);

  readonly humaniseActorType = humaniseActorType;
  readonly humaniseEventType = humaniseEventType;

  readonly canExport = computed(() =>
    this.auth.hasPermission(Permissions.ExportAuditTrail),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.entries().length === 0,
  );

  constructor() {
    this.fetchFirstPage();
  }

  private currentFilter(): AuditTrailFilter {
    const v = this.filters.getRawValue();
    return {
      actorUserId: v.actorUserId.trim() || undefined,
      eventType: v.eventType.trim() || undefined,
      targetObjectType: v.targetObjectType.trim() || undefined,
      targetObjectId: v.targetObjectId.trim() || undefined,
      dateFrom: toIsoStart(v.dateFrom),
      dateTo: toIsoEnd(v.dateTo),
    };
  }

  apply(): void {
    this.fetchFirstPage();
  }

  clear(): void {
    this.filters.reset({
      actorUserId: '',
      eventType: '',
      targetObjectType: '',
      targetObjectId: '',
      dateFrom: null,
      dateTo: null,
    });
    this.fetchFirstPage();
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.entries.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.entries.set(items);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.state.set('ready');
    });
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.query(this.nextCursor(), (items, cursor, more) => {
      this.entries.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: AuditTrailEntry[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    this.service
      .query(this.currentFilter(), cursor ?? undefined, PAGE_SIZE)
      .subscribe({
        next: (page) => onSuccess(page.items, page.nextCursor, page.hasMore),
        error: () => {
          if (cursor === null) {
            this.state.set('error');
          } else {
            this.loadingMore.set(false);
          }
        },
      });
  }

  open(entry: AuditTrailEntry): void {
    const data: AuditTrailDetailDialogData = { entry };
    this.dialog.open(AuditTrailDetailDialogComponent, {
      data,
      width: '760px',
      maxWidth: '95vw',
    });
  }

  exportCsv(): void {
    if (this.exporting()) {
      return;
    }
    this.exporting.set(true);
    this.service.exportCsv(this.currentFilter()).subscribe({
      next: (response) => {
        this.exporting.set(false);
        const blob = response.body;
        if (!blob) {
          this.notify.error('The export returned no content.');
          return;
        }
        this.triggerDownload(blob);
        const sha = response.headers.get('X-Content-SHA256') ?? 'unknown';
        this.notify.success(`Exported (SHA-256: ${sha})`);
      },
      error: () => {
        this.exporting.set(false);
        this.notify.error('We could not export the audit trail.');
      },
    });
  }

  private triggerDownload(blob: Blob): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `audit-trail-${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  }
}
