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
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { Permissions } from '../../../core/permissions';
import { AuditTrailEntry, AuditTrailFilter } from '../../../core/models';
import { humaniseActorType, humaniseEventType } from './humanise';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';
import {
  AuditTrailDetailDialogComponent,
  AuditTrailDetailDialogData,
} from './dialogs/audit-trail-detail-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

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
    SearchableSelectComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    TranslatePipe,
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
  private readonly i18n = inject(TranslationService);
  private readonly userLookup = inject(UserLookupService);

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
    dateFrom: null as Date | null,
    dateTo: null as Date | null,
  });

  readonly state = signal<ViewState>('loading');
  readonly entries = signal<AuditTrailEntry[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);
  readonly exporting = signal(false);

  /** Distinct event/target-type vocabularies from the backend, for the dropdowns. */
  private readonly facets = signal<{ eventTypes: string[]; targetTypes: string[] }>({
    eventTypes: [],
    targetTypes: [],
  });

  readonly humaniseActorType = humaniseActorType;
  readonly humaniseEventType = humaniseEventType;

  /** Actor dropdown: an "all" sentinel plus every known user, resolved to a display name. */
  readonly actorOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('adminMisc.auditTrail.filter.allActors') },
    ...this.userLookup
      .options()
      .map((u) => ({ value: u.id, label: u.displayName || u.id })),
  ]);

  readonly eventTypeOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('adminMisc.auditTrail.filter.allEvents') },
    ...this.facets().eventTypes.map((t) => ({ value: t, label: humaniseEventType(t) })),
  ]);

  readonly targetTypeOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('adminMisc.auditTrail.filter.allTargets') },
    ...this.facets().targetTypes.map((t) => ({ value: t, label: humaniseEventType(t) })),
  ]);

  readonly canExport = computed(() =>
    this.auth.hasPermission(Permissions.ExportAuditTrail),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.entries().length === 0,
  );

  constructor() {
    this.userLookup.ensureLoaded();
    this.loadFacets();
    this.fetchPage(1);
  }

  /** Resolves the actor of an entry to a human-readable name (user directory → system label → id → em dash). */
  actorName(entry: AuditTrailEntry): string {
    if (entry.actorUserId) {
      return this.userLookup.displayName(entry.actorUserId);
    }
    return entry.actorSystemLabel ?? '—';
  }

  /** Abbreviates a GUID target id to its leading segment; the detail dialog carries the full value. */
  shortId(id: string | null): string {
    if (!id) {
      return '—';
    }
    const head = id.split('-')[0];
    return head ? `${head}…` : id;
  }

  private loadFacets(): void {
    this.service.facets().subscribe({
      next: (f) => this.facets.set({ eventTypes: f.eventTypes, targetTypes: f.targetTypes }),
      // Non-fatal: leave the dropdowns with just the "all" option on error.
      error: () => undefined,
    });
  }

  private currentFilter(): AuditTrailFilter {
    const v = this.filters.getRawValue();
    return {
      actorUserId: v.actorUserId || undefined,
      eventType: v.eventType || undefined,
      targetObjectType: v.targetObjectType || undefined,
      dateFrom: toIsoStart(v.dateFrom),
      dateTo: toIsoEnd(v.dateTo),
    };
  }

  apply(): void {
    this.fetchPage(1);
  }

  clear(): void {
    this.filters.reset({
      actorUserId: '',
      eventType: '',
      targetObjectType: '',
      dateFrom: null,
      dateTo: null,
    });
    this.fetchPage(1);
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.service
      .query(this.currentFilter(), page, this.pageSize())
      .subscribe({
        next: (result) => {
          this.entries.set(result.items);
          this.total.set(result.total);
          this.page.set(result.page);
          this.state.set('ready');
          this.loading.set(false);
        },
        error: () => {
          if (this.state() === 'loading') {
            this.state.set('error');
          }
          this.loading.set(false);
        },
      });
  }

  onPageChange(page: number): void {
    this.fetchPage(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.fetchPage(1);
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
          this.notify.error(
            this.i18n.translate('adminMisc.auditTrail.export.noContent'),
          );
          return;
        }
        this.triggerDownload(blob);
        const sha = response.headers.get('X-Content-SHA256') ?? 'unknown';
        this.notify.success(
          this.i18n.translate('adminMisc.auditTrail.export.success', { sha }),
        );
      },
      error: () => {
        this.exporting.set(false);
        this.notify.error(
          this.i18n.translate('adminMisc.auditTrail.export.error'),
        );
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
