import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';

import { GlobalSearchComponent } from './global-search.component';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

/** The component debounces at 250ms with real timers (the app is zoneless). */
const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

describe('GlobalSearchComponent', () => {
  let fixture: ComponentFixture<GlobalSearchComponent>;
  let component: GlobalSearchComponent;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [GlobalSearchComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(GlobalSearchComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('does not query for terms shorter than 2 characters', async () => {
    component.query.setValue('a');
    await wait(300);
    http.expectNone((r) => r.url === `${BASE}/search`);
    expect(component.groups().length).toBe(0);
  });

  it('debounces, groups hits by type in order, and preserves a flat index', async () => {
    component.query.setValue('q1');
    await wait(300);
    const req = http.expectOne((r) => r.url === `${BASE}/search`);
    expect(req.request.params.get('q')).toBe('q1');
    req.flush({
      data: {
        hits: [
          { type: 'user', id: 'u1', title: 'Jo Smith', subtitle: 'jo@x' },
          { type: 'audit', id: 'a1', title: 'Q1 Audit', subtitle: 'Draft' },
        ],
      },
    });

    const groups = component.groups();
    // 'audit' sorts before 'user' regardless of response order.
    expect(groups.map((g) => g.type)).toEqual(['audit', 'user']);
    expect(groups[0].hits[0].index).toBe(0);
    expect(groups[1].hits[0].index).toBe(1);
    expect(component.open()).toBe(true);
  });

  it('navigates to the audit detail on selection and closes', async () => {
    const navSpy = spyOn(router, 'navigate');
    component.query.setValue('q1');
    await wait(300);
    http.expectOne((r) => r.url === `${BASE}/search`).flush({
      data: { hits: [{ type: 'audit', id: 'a1', title: 'Q1 Audit', subtitle: 'Draft' }] },
    });

    component.go(component.groups()[0].hits[0]);
    expect(navSpy).toHaveBeenCalledWith(['/audits', 'a1']);
    expect(component.open()).toBe(false);
  });

  it('clears the query and results', async () => {
    component.query.setValue('q1');
    await wait(300);
    http.expectOne((r) => r.url === `${BASE}/search`).flush({
      data: { hits: [{ type: 'audit', id: 'a1', title: 'Q1 Audit', subtitle: null }] },
    });

    component.clear();
    expect(component.query.value).toBe('');
    expect(component.groups().length).toBe(0);
    expect(component.open()).toBe(false);
  });
});
