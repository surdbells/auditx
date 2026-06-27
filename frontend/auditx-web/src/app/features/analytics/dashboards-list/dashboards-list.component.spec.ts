import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { DashboardsListComponent } from './dashboards-list.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { DashboardListItem } from '../../../core/models';

const BASE = '/api/v1';

function listItem(overrides: Partial<DashboardListItem> = {}): DashboardListItem {
  return {
    id: 'd-1',
    slug: 'function-performance',
    name: 'Function performance',
    description: 'Plan execution and exception throughput.',
    permissionRequired: null,
    configurationVersion: 1,
    widgetCount: 3,
    ...overrides,
  };
}

describe('DashboardsListComponent', () => {
  let fixture: ComponentFixture<DashboardsListComponent>;
  let component: DashboardsListComponent;
  let http: HttpTestingController;

  async function setup(items: DashboardListItem[]): Promise<void> {
    TestBed.configureTestingModule({
      imports: [DashboardsListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(DashboardsListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne(`${BASE}/dashboards`).flush({ data: items });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('lists the dashboards the caller may see', async () => {
    await setup([listItem(), listItem({ id: 'd-2', name: 'Exceptions' })]);
    expect(component.dashboards().length).toBe(2);
    expect(component.state()).toBe('ready');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Function performance');
    expect(text).toContain('3 widgets');
  });

  it('shows the empty state when no dashboards are returned', async () => {
    await setup([]);
    expect(component.isEmpty()).toBe(true);
  });

  it('surfaces an error state when the list call fails', async () => {
    TestBed.configureTestingModule({
      imports: [DashboardsListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(DashboardsListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http
      .expectOne(`${BASE}/dashboards`)
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    expect(component.state()).toBe('error');
  });
});
