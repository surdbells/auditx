import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { ReportsPanelComponent } from './reports-panel.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  Audit,
  AuditStatus,
  PagedResult,
  Report,
  ReportListItem,
  SessionDto,
} from '../../../core/models';

const BASE = '/api/v1';

/** Builds an offset-paged envelope mirroring the backend `PagedResult<T>`. */
function pagedResult<T>(
  items: T[],
  total = items.length,
  pageNum = 1,
  pageSize = 25,
): PagedResult<T> {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  return {
    items,
    total,
    page: pageNum,
    pageSize,
    totalPages,
    hasPrevious: pageNum > 1,
    hasNext: pageNum < totalPages,
  };
}

function session(permissions: string[]): SessionDto {
  return {
    userId: 'u1',
    email: 'a@b.c',
    firstName: 'A',
    lastName: 'B',
    displayName: 'A B',
    status: 'active',
    roles: [],
    permissions,
    expiresAt: '',
    absoluteExpiresAt: '',
  };
}

function audit(status: AuditStatus = 'under_review'): Audit {
  return {
    id: 'au-1',
    name: 'AML Review',
    scopeDescription: null,
    auditType: 'AML',
    status,
    startDate: '2026-01-01',
    targetEndDate: null,
    actualEndDate: null,
    templateId: null,
    templateVersion: null,
    planItemId: null,
    leadUserId: 'u-lead',
    auditeeUserId: 'u-auditee',
    cancellationReason: null,
    budgetedHours: null,
    isSelfAssessment: false,
    version: 'v1',
    teamMembers: [],
    sections: [],
    checklistItems: [],
  };
}

function listItem(overrides: Partial<ReportListItem> = {}): ReportListItem {
  return {
    id: 'r-1',
    auditId: 'au-1',
    kind: 'audit_engagement',
    versionNumber: 1,
    status: 'completed',
    sha256Hash: 'deadbeefcafef00d',
    producedFormats: ['html'],
    generatedBy: 'u-1',
    requestedAt: '2026-06-01T10:00:00Z',
    completedAt: '2026-06-01T10:01:00Z',
    ...overrides,
  };
}

function report(overrides: Partial<Report> = {}): Report {
  return {
    id: 'r-9',
    auditId: 'au-1',
    kind: 'audit_engagement',
    versionNumber: 2,
    status: 'pending',
    sha256Hash: null,
    templateId: 't-1',
    templateVersionSnapshot: 1,
    requestedFormats: ['html'],
    producedArtefacts: [],
    failureReason: null,
    generatedBy: 'u-1',
    requestedAt: '2026-06-02T10:00:00Z',
    completedAt: null,
    version: 'v1',
    ...overrides,
  };
}

describe('ReportsPanelComponent', () => {
  let fixture: ComponentFixture<ReportsPanelComponent>;
  let component: ReportsPanelComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;

  async function setup(
    perms: string[],
    auditStatus: AuditStatus,
    items: ReportListItem[],
  ): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);

    TestBed.configureTestingModule({
      imports: [ReportsPanelComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(ReportsPanelComponent);
    fixture.componentRef.setInput('auditId', 'au-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // fetch runs in a microtask: audit then version list.
    await fixture.whenStable();
    http.expectOne(`${BASE}/audits/au-1`).flush({ data: audit(auditStatus) });
    await fixture.whenStable();
    http
      .expectOne((r) => r.url === `${BASE}/audits/au-1/reports`)
      .flush({ data: pagedResult(items) });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads the audit status and the version list', async () => {
    await setup(['ViewReport', 'GenerateReport'], 'under_review', [listItem()]);
    expect(component.reports().length).toBe(1);
    expect(component.canGenerateNow()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Completed');
  });

  it('disables generation when the audit is still in progress', async () => {
    await setup(['ViewReport', 'GenerateReport'], 'in_progress', []);
    expect(component.canGenerateNow()).toBe(false);
  });

  it('polls until the generation completes, then refreshes the list', async () => {
    await setup(['ViewReport', 'GenerateReport'], 'completed', []);

    // Drive the ~2s poll loop deterministically. The HTTP flushes and signal
    // updates are synchronous in this zoneless app, so no whenStable is needed
    // between ticks.
    jasmine.clock().install();
    try {
      component.generate();

      // POST 202 returns the new report id (pending).
      http
        .expectOne(`${BASE}/audits/au-1/reports`)
        .flush(
          { data: { reportId: 'r-9', status: 'pending' } },
          { status: 202, statusText: 'Accepted' },
        );
      expect(component.generating()).toBe(true);

      // First poll fires at delay 0 (timer(0, …)): still running.
      jasmine.clock().tick(0);
      http
        .expectOne(`${BASE}/reports/r-9`)
        .flush({ data: report({ status: 'running' }) });

      // Second poll after the interval: completed → refresh list.
      jasmine.clock().tick(2000);
      http
        .expectOne(`${BASE}/reports/r-9`)
        .flush({ data: report({ status: 'completed' }) });
      http
        .expectOne((r) => r.url === `${BASE}/audits/au-1/reports`)
        .flush({
          data: pagedResult([listItem({ id: 'r-9', versionNumber: 2 })]),
        });
    } finally {
      jasmine.clock().uninstall();
    }

    expect(component.generating()).toBe(false);
    expect(notify.success).toHaveBeenCalled();
    expect(component.reports().length).toBe(1);
  });

  it('navigates to another page via the paginator, carrying page + pageSize', async () => {
    await setup(['ViewReport'], 'completed', [listItem()]);

    component.onPageChange(2);
    const req = http.expectOne((r) => r.url === `${BASE}/audits/au-1/reports`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({
      data: pagedResult([listItem({ id: 'r-2', versionNumber: 2 })], 50, 2),
    });

    expect(component.reports().map((r) => r.id)).toEqual(['r-2']);
    expect(component.page()).toBe(2);
  });

  it('changing the page size re-queries from page 1', async () => {
    await setup(['ViewReport'], 'completed', [listItem()]);

    component.onPageSizeChange(100);
    const req = http.expectOne((r) => r.url === `${BASE}/audits/au-1/reports`);
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('100');
    req.flush({ data: pagedResult([listItem()], 50, 1, 100) });
    expect(component.pageSize()).toBe(100);
  });

  it('humanises report statuses', async () => {
    await setup(['ViewReport'], 'completed', []);
    expect(component.humanise('in_progress')).toBe('In progress');
    expect(component.humanise(null)).toBe('—');
  });
});
