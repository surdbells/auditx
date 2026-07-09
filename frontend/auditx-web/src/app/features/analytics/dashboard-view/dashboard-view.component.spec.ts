import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';

import { DashboardViewComponent } from './dashboard-view.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  DashboardDetail,
  DashboardWidget,
  SessionDto,
} from '../../../core/models';

const BASE = '/api/v1';

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

function widget(overrides: Partial<DashboardWidget> = {}): DashboardWidget {
  return {
    id: 'w-1',
    widgetType: 'single_metric',
    metricKey: 'function_performance',
    title: 'Plan execution',
    targetRoleId: null,
    position: 1,
    configJson: null,
    data: null,
    version: 'v1',
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
    configurationVersion: 1,
    widgets: [],
    version: 'v1',
    ...overrides,
  };
}

describe('DashboardViewComponent', () => {
  let fixture: ComponentFixture<DashboardViewComponent>;
  let component: DashboardViewComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  async function setup(
    perms: string[],
    dash: DashboardDetail,
  ): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    TestBed.configureTestingModule({
      imports: [DashboardViewComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(DashboardViewComponent);
    fixture.componentRef.setInput('idOrSlug', 'function-performance');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // fetch runs in a microtask.
    await fixture.whenStable();
    http
      .expectOne(`${BASE}/dashboards/function-performance`)
      .flush({ data: dash });
    await fixture.whenStable();
    fixture.detectChanges();

    // Table widgets resolve GUID columns via the user/entity lookup services,
    // which lazily fetch the directory + entity list. Flush whichever fired
    // (match() is a no-op when a dashboard has no id-bearing table widgets).
    for (const req of http.match((r) => r.url === `${BASE}/users/directory`)) {
      req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    }
    for (const req of http.match((r) => r.url === `${BASE}/audit-universe/entities`)) {
      req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    }
  }

  afterEach(() => http.verify());

  it('loads the dashboard and sorts widgets by position', async () => {
    await setup(['ViewAnalytics'], {
      ...detail(),
      widgets: [
        widget({ id: 'w-2', position: 2, title: 'Second' }),
        widget({ id: 'w-1', position: 1, title: 'First' }),
      ],
    });
    expect(component.widgets().map((w) => w.id)).toEqual(['w-1', 'w-2']);
  });

  it('renders single_metric key figures from a non-gauge payload', async () => {
    await setup(['ViewAnalytics'], {
      ...detail(),
      widgets: [
        widget({
          widgetType: 'single_metric',
          data: { openExceptionBacklog: 3, closedExceptions: 12 },
        }),
      ],
    });
    // No gauge-triggering percentage, so the figure grid renders.
    expect(component.gaugeMetric(component.widgets()[0])).toBeNull();
    const figures = component.keyFigures(component.widgets()[0]);
    expect(figures.length).toBe(2);
    const backlog = figures.find((f) => f.label === 'Open Exception Backlog');
    expect(backlog?.value).toBe('3');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('3');
    expect(text).toContain('12');
  });

  it('projects an array payload into a generic table', async () => {
    await setup(['ViewAnalytics'], {
      ...detail(),
      widgets: [
        widget({
          widgetType: 'table',
          data: [
            { businessUnit: 'Retail', caseCount: 4, appealRatePercent: 25 },
            { businessUnit: 'Corporate', caseCount: 2, appealRatePercent: 0 },
          ],
        }),
      ],
    });
    const table = component.table(component.widgets()[0]);
    expect(table.columns).toContain('businessUnit');
    expect(table.rows.length).toBe(2);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Retail');
  });

  it('renders a bySeverity chart widget as a donut chart', async () => {
    await setup(['ViewAnalytics'], {
      ...detail(),
      widgets: [
        widget({
          widgetType: 'chart',
          data: {
            bySeverity: [
              { severity: 'critical', count: 2 },
              { severity: 'high', count: 5 },
            ],
          },
        }),
      ],
    });
    const w = component.widgets()[0];
    const bars = component.bars(w);
    expect(bars.map((b) => b.label)).toEqual(['Critical', 'High']);
    // A severity breakdown is a parts-of-a-whole → donut.
    expect(component.chartKind(w)).toBe('donut');
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('app-donut-chart')).toBeTruthy();
    // The donut ring is drawn from one stroked circle per slice (+1 track).
    expect(host.querySelectorAll('app-donut-chart circle').length).toBe(3);
  });

  it('renders a non-severity chart widget (age buckets) as a bar chart', async () => {
    await setup(['ViewAnalytics'], {
      ...detail(),
      widgets: [
        widget({
          widgetType: 'chart',
          data: {
            byAgeBucket: [
              { bucket: '0_30', count: 4 },
              { bucket: '31_60', count: 1 },
            ],
          },
        }),
      ],
    });
    const w = component.widgets()[0];
    expect(component.chartKind(w)).toBe('bar');
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('app-bar-chart')).toBeTruthy();
    // One filled bar rect (plus its track) per datum.
    expect(host.querySelectorAll('app-bar-chart rect').length).toBe(4);
  });

  it('renders a percentage single_metric as a gauge', async () => {
    await setup(['ViewAnalytics'], {
      ...detail(),
      widgets: [
        widget({
          widgetType: 'single_metric',
          data: { planExecutionPercent: 70, openExceptionBacklog: 3 },
        }),
      ],
    });
    const w = component.widgets()[0];
    expect(component.gaugeMetric(w)).toEqual({
      value: 70,
      label: 'Plan Execution',
    });
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('app-gauge-chart')).toBeTruthy();
    expect((host.textContent ?? '').includes('70%')).toBe(true);
  });

  it('renders an empty shell for a widget whose data is null (degraded/forbidden)', async () => {
    await setup(['ViewAnalytics'], {
      ...detail(),
      widgets: [widget({ data: null, title: 'Sanctions consistency' })],
    });
    expect(component.isUnavailable(component.widgets()[0])).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('not available');
  });

  it('hides admin controls without ConfigureDashboards', async () => {
    await setup(['ViewAnalytics'], { ...detail(), widgets: [widget()] });
    expect(component.canConfigure()).toBe(false);
  });

  it('adds a widget and refreshes from the returned detail', async () => {
    await setup(['ViewAnalytics', 'ConfigureDashboards'], detail());
    expect(component.canConfigure()).toBe(true);

    dialog.open.and.returnValue({
      afterClosed: () =>
        of({
          widgetType: 'single_metric',
          metricKey: 'function_performance',
          title: 'New widget',
          position: 1,
          targetRoleId: null,
          configJson: null,
        }),
    } as ReturnType<MatDialog['open']>);

    component.addWidget();

    const req = http.expectOne(`${BASE}/dashboards/d-1/widgets`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.version).toBe('v1');
    expect(req.request.body.title).toBe('New widget');
    req.flush(
      { data: detail({ version: 'v2', widgets: [widget({ title: 'New widget' })] }) },
      { status: 201, statusText: 'Created' },
    );

    expect(component.dashboard()?.version).toBe('v2');
    expect(component.widgets().length).toBe(1);
    expect(notify.success).toHaveBeenCalled();
  });

  it('removes a widget after confirmation, sending the rowversion', async () => {
    await setup(['ViewAnalytics', 'ConfigureDashboards'], {
      ...detail(),
      widgets: [widget()],
    });

    dialog.open.and.returnValue({
      afterClosed: () => of(true),
    } as ReturnType<MatDialog['open']>);

    component.deleteWidget(component.widgets()[0]);

    const req = http.expectOne(`${BASE}/dashboards/d-1/widgets/w-1`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: detail({ version: 'v2', widgets: [] }) });

    expect(component.dashboard()?.version).toBe('v2');
    expect(component.widgets().length).toBe(0);
    expect(notify.success).toHaveBeenCalled();
  });
});
