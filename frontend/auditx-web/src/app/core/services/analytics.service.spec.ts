import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AnalyticsService } from './analytics.service';
import { provideTestEnv } from '../../../testing/test-providers';
import {
  DashboardDetail,
  DashboardListItem,
  RecurrenceCluster,
  SaveDashboardWidgetRequest,
} from '../models';

const BASE = '/api/v1';

function listItem(overrides: Partial<DashboardListItem> = {}): DashboardListItem {
  return {
    id: 'd-1',
    slug: 'function-performance',
    name: 'Function performance',
    description: 'KPIs',
    permissionRequired: null,
    configurationVersion: 1,
    widgetCount: 3,
    ...overrides,
  };
}

function detail(overrides: Partial<DashboardDetail> = {}): DashboardDetail {
  return {
    id: 'd-1',
    slug: 'function-performance',
    name: 'Function performance',
    description: 'KPIs',
    permissionRequired: null,
    configurationVersion: 2,
    widgets: [],
    version: 'v1',
    ...overrides,
  };
}

function cluster(overrides: Partial<RecurrenceCluster> = {}): RecurrenceCluster {
  return {
    id: 'c-1',
    auditableEntityId: 'e-1',
    category: 'aml',
    closedExceptionCount: 4,
    windowMonths: 12,
    firstOccurredAt: '2026-01-01T00:00:00Z',
    lastOccurredAt: '2026-05-01T00:00:00Z',
    detectedAt: '2026-06-01T00:00:00Z',
    notifiedAt: null,
    ...overrides,
  };
}

describe('AnalyticsService', () => {
  let service: AnalyticsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(AnalyticsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists dashboards and unwraps the envelope', () => {
    let result: DashboardListItem[] | undefined;
    service.listDashboards().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/dashboards`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [listItem()] });
    expect(result?.length).toBe(1);
    expect(result?.[0].widgetCount).toBe(3);
  });

  it('fetches a dashboard by slug', () => {
    let result: DashboardDetail | undefined;
    service.getDashboard('function-performance').subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/dashboards/function-performance`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: detail() });
    expect(result?.version).toBe('v1');
  });

  it('adds a widget with the dashboard version and returns the refreshed detail', () => {
    const body: SaveDashboardWidgetRequest = {
      widgetType: 'single_metric',
      metricKey: 'function_performance',
      title: 'Plan execution',
      position: 1,
      version: 'v1',
    };
    let result: DashboardDetail | undefined;
    service.addWidget('d-1', body).subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/dashboards/d-1/widgets`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.metricKey).toBe('function_performance');
    expect(req.request.body.version).toBe('v1');
    req.flush(
      { data: detail({ configurationVersion: 3, version: 'v2' }) },
      { status: 201, statusText: 'Created' },
    );
    expect(result?.version).toBe('v2');
  });

  it('updates a widget via PATCH', () => {
    const body: SaveDashboardWidgetRequest = {
      widgetType: 'table',
      metricKey: 'coverage',
      title: 'Coverage',
      position: 2,
      version: 'v2',
    };
    let result: DashboardDetail | undefined;
    service.updateWidget('d-1', 'w-1', body).subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/dashboards/d-1/widgets/w-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.widgetType).toBe('table');
    req.flush({ data: detail() });
    expect(result?.id).toBe('d-1');
  });

  it('deletes a widget via DELETE with the version in the body', () => {
    let result: DashboardDetail | undefined;
    service
      .deleteWidget('d-1', 'w-1', { version: 'v2' })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/dashboards/d-1/widgets/w-1`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body.version).toBe('v2');
    expect(req.request.withCredentials).toBe(true);
    req.flush({ data: detail({ widgets: [] }) });
    expect(result).toBeDefined();
  });

  it('fetches function-performance KPIs', () => {
    let result: { planExecutionPercent: number } | undefined;
    service.functionPerformance().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/analytics/function-performance`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: {
        auditsInFlight: 2,
        auditsCompleted: 5,
        planItemsTotal: 10,
        planItemsCompleted: 7,
        planExecutionPercent: 70,
        openExceptionBacklog: 3,
        closedExceptions: 12,
        closureRatePercent: 80,
      },
    });
    expect(result?.planExecutionPercent).toBe(70);
  });

  it('fetches the exception portfolio', () => {
    let result: { totalOpen: number } | undefined;
    service.exceptionPortfolio().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/analytics/exception-portfolio`);
    req.flush({
      data: {
        totalOpen: 3,
        bySeverity: [],
        byAgeBucket: [],
        byEntity: [],
        averageClosureDays: null,
      },
    });
    expect(result?.totalOpen).toBe(3);
  });

  it('fetches sanctions consistency (aggregated, no subject identity)', () => {
    let result: { totalCases: number } | undefined;
    service.sanctionsConsistency().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/analytics/sanctions-consistency`);
    req.flush({
      data: {
        totalCases: 0,
        overallGridAdherencePercent: 0,
        overallAppealRatePercent: 0,
        byBusinessUnit: [],
      },
    });
    expect(result?.totalCases).toBe(0);
  });

  it('fetches material findings', () => {
    let result: unknown[] | undefined;
    service.materialFindings().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/analytics/material-findings`);
    req.flush({ data: [] });
    expect(result).toEqual([]);
  });

  it('fetches plan status', () => {
    let result: { completionPercent: number } | undefined;
    service.planStatus().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/analytics/plan-status`);
    req.flush({
      data: {
        totalPlans: 1,
        totalItems: 5,
        planned: 1,
        inProgress: 2,
        completed: 2,
        deferred: 0,
        completionPercent: 40,
      },
    });
    expect(result?.completionPercent).toBe(40);
  });

  it('fetches the coverage matrix with the windowMonths param', () => {
    let result: { rows: unknown[] } | undefined;
    service.coverage(6).subscribe((r) => (result = r));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/analytics/coverage`,
    );
    expect(req.request.params.get('windowMonths')).toBe('6');
    req.flush({ data: { rows: [], columns: [], cells: [] } });
    expect(result?.rows).toEqual([]);
  });

  it('fetches performance scorecards', () => {
    let result: unknown[] | undefined;
    service.performanceScorecards().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/analytics/performance-scorecards`);
    req.flush({ data: [] });
    expect(result).toEqual([]);
  });

  it('lists recurrence clusters with page + pageSize and unwraps the page', () => {
    let result: { items: RecurrenceCluster[] } | undefined;
    service
      .recurrenceClusters(2, 20)
      .subscribe((page) => (result = page));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/analytics/recurrence-clusters`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush({
      data: {
        items: [cluster()],
        total: 1,
        page: 2,
        pageSize: 20,
        totalPages: 1,
        hasPrevious: true,
        hasNext: false,
      },
    });
    expect(result?.items.length).toBe(1);
  });

  it('fetches a recurrence cluster detail', () => {
    let result: { id: string; members: unknown[] } | undefined;
    service.recurrenceClusterDetail('c-1').subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/analytics/recurrence-clusters/c-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: { ...cluster(), members: [] } });
    expect(result?.id).toBe('c-1');
    expect(result?.members).toEqual([]);
  });
});
