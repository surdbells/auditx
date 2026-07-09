import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime } from 'rxjs';

import { ExceptionsService } from '../../../core/services/exceptions.service';
import { UsersService } from '../../../core/services/users.service';
import { AnnualPlansService } from '../../../core/services/annual-plans.service';
import { AuditsService } from '../../../core/services/audits.service';
import {
  AuditListItem,
  ExceptionListItem,
  ExceptionQuery,
  ExceptionSeverity,
  ExceptionStatus,
  PlanListItem,
  UserDto,
} from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 7;
/** Page size while eagerly caching the plan/audit directories for the dropdowns (max allowed by the API). */
const LOOKUP_LIMIT = 100;
/** Safety cap so a runaway cursor never loops forever. */
const MAX_LOOKUP_PAGES = 25;

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
  selector: 'app-exceptions-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    SearchableSelectComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './exceptions-list.component.html',
  styleUrl: './exceptions-list.component.scss',
})
export class ExceptionsListComponent {
  private readonly service = inject(ExceptionsService);
  private readonly users = inject(UsersService);
  private readonly plans = inject(AnnualPlansService);
  private readonly audits = inject(AuditsService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'title',
    'audit',
    'severity',
    'status',
    'owner',
    'raised',
    'targetDate',
    'flags',
  ];

  readonly statuses: { value: ExceptionStatus | 'all'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.allStatuses') },
    { value: 'open', label: this.i18n.translate('exceptions.status.open') },
    { value: 'map_submitted', label: this.i18n.translate('exceptions.status.mapSubmitted') },
    { value: 'map_approved', label: this.i18n.translate('exceptions.status.mapApproved') },
    { value: 'map_rejected', label: this.i18n.translate('exceptions.status.mapRejected') },
    { value: 'pending_closure', label: this.i18n.translate('exceptions.status.pendingClosure') },
    { value: 'closed', label: this.i18n.translate('exceptions.status.closed') },
    { value: 'cancelled', label: this.i18n.translate('exceptions.status.cancelled') },
  ];

  readonly severities: { value: ExceptionSeverity | 'all'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.allSeverities') },
    { value: 'low', label: this.i18n.translate('exceptions.severity.low') },
    { value: 'medium', label: this.i18n.translate('exceptions.severity.medium') },
    { value: 'high', label: this.i18n.translate('exceptions.severity.high') },
    { value: 'critical', label: this.i18n.translate('exceptions.severity.critical') },
  ];

  readonly boolFilters: { value: 'all' | 'yes' | 'no'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.all') },
    { value: 'yes', label: this.i18n.translate('exceptions.filter.yes') },
    { value: 'no', label: this.i18n.translate('exceptions.filter.no') },
  ];

  readonly filters = this.fb.nonNullable.group({
    search: '',
    status: 'all' as ExceptionStatus | 'all',
    severity: 'all' as ExceptionSeverity | 'all',
    plan: '',
    audit: '',
    overdue: 'all' as 'all' | 'yes' | 'no',
    recurrence: 'all' as 'all' | 'yes' | 'no',
    raisedFrom: null as Date | null,
    raisedTo: null as Date | null,
  });

  readonly state = signal<ViewState>('loading');
  readonly exceptions = signal<ExceptionListItem[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);
  readonly exporting = signal(false);
  readonly userNames = signal<Record<string, string>>({});
  private readonly planList = signal<PlanListItem[]>([]);
  private readonly auditList = signal<AuditListItem[]>([]);

  /** Plan dropdown: an "all" sentinel plus every annual plan, labelled by period. */
  readonly planOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('exceptions.filter.allPlans') },
    ...this.planList().map((p) => ({ value: p.id, label: p.periodLabel })),
  ]);

  /** Audit dropdown: an "all" sentinel plus every audit, labelled by name. */
  readonly auditOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('exceptions.filter.allAudits') },
    ...this.auditList().map((a) => ({ value: a.id, label: a.name })),
  ]);

  private readonly auditNames = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {};
    for (const a of this.auditList()) {
      map[a.id] = a.name;
    }
    return map;
  });

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.exceptions().length === 0,
  );

  private usersCache: UserDto[] = [];

  constructor() {
    this.ensureUsers();
    this.loadPlans(null, 0);
    this.loadAudits(null, 0);
    this.fetchFirstPage();
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchFirstPage());
  }

  private ensureUsers(): void {
    this.users.list({ status: 'active', limit: 200 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        const map: Record<string, string> = {};
        for (const u of page.items) {
          map[u.id] = u.displayName;
        }
        this.userNames.set(map);
      },
      error: () => {
        // Non-fatal: ids display verbatim.
      },
    });
  }

  /** Eagerly pages the annual-plan directory into the plan dropdown. Non-fatal on error. */
  private loadPlans(cursor: string | null, pageIndex: number): void {
    if (pageIndex >= MAX_LOOKUP_PAGES) {
      return;
    }
    this.plans.list({ limit: LOOKUP_LIMIT, cursor }).subscribe({
      next: (page) => {
        this.planList.update((prev) => [...prev, ...page.items]);
        if (page.hasMore && page.nextCursor) {
          this.loadPlans(page.nextCursor, pageIndex + 1);
        }
      },
      error: () => undefined,
    });
  }

  /** Eagerly pages the audit directory into the audit dropdown + name map. Non-fatal on error. */
  private loadAudits(cursor: string | null, pageIndex: number): void {
    if (pageIndex >= MAX_LOOKUP_PAGES) {
      return;
    }
    this.audits.list({ limit: LOOKUP_LIMIT, cursor }).subscribe({
      next: (page) => {
        this.auditList.update((prev) => [...prev, ...page.items]);
        if (page.hasMore && page.nextCursor) {
          this.loadAudits(page.nextCursor, pageIndex + 1);
        }
      },
      error: () => undefined,
    });
  }

  nameOf(userId: string | null | undefined): string {
    if (!userId) {
      return '—';
    }
    return this.userNames()[userId] ?? userId;
  }

  /** Resolves an audit id to its name for the grid; falls back to the raw id while unresolved. */
  auditNameOf(auditId: string | null | undefined): string {
    if (!auditId) {
      return '—';
    }
    return this.auditNames()[auditId] ?? auditId;
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.exceptions.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.exceptions.set(items);
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
      this.exceptions.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  /** The current filter selection as an ExceptionQuery (no pagination) — shared by the list query and the CSV export. */
  private buildFilter(): ExceptionQuery {
    const { search, status, severity, plan, audit, overdue, recurrence, raisedFrom, raisedTo } =
      this.filters.getRawValue();
    return {
      search: search.trim() || undefined,
      status: status === 'all' ? '' : status,
      severity: severity === 'all' ? '' : severity,
      plan: plan || undefined,
      audit: audit || undefined,
      overdue: overdue === 'all' ? undefined : overdue === 'yes',
      recurrence: recurrence === 'all' ? undefined : recurrence === 'yes',
      raisedFrom: toIsoStart(raisedFrom),
      raisedTo: toIsoEnd(raisedTo),
    };
  }

  /** Downloads the current filtered finding register as a CSV file. */
  exportCsv(): void {
    if (this.exporting()) {
      return;
    }
    this.exporting.set(true);
    this.service.exportRegister(this.buildFilter()).subscribe({
      next: (response) => {
        this.exporting.set(false);
        const blob = response.body;
        if (!blob) {
          return;
        }
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `finding-register-${new Date().toISOString().slice(0, 10)}.csv`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.exporting.set(false),
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: ExceptionListItem[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    this.service
      .list({ ...this.buildFilter(), cursor, limit: PAGE_SIZE })
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

  open(row: ExceptionListItem): void {
    void this.router.navigate(['/exceptions', row.id]);
  }
}
