import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { ReferenceDataComponent } from './reference-data.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { ReferenceDataItem, SessionDto } from '../../../core/models';

const BASE = '/api/v1';

function item(
  code: string,
  label: string,
  overrides: Partial<ReferenceDataItem> = {},
): ReferenceDataItem {
  return {
    id: `r-${code}`,
    category: 'audit_type',
    code,
    label,
    sortOrder: 0,
    isActive: true,
    ...overrides,
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

describe('ReferenceDataComponent', () => {
  let fixture: ComponentFixture<ReferenceDataComponent>;
  let component: ReferenceDataComponent;
  let http: HttpTestingController;

  function setup(permissions: string[] = ['ViewConfig', 'ManageConfiguration']): void {
    TestBed.configureTestingModule({
      imports: [ReferenceDataComponent],
      providers: [provideTestEnv()],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(permissions));

    fixture = TestBed.createComponent(ReferenceDataComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  /** Flushes the categories catalogue then the first category's item list. */
  function flushInitial(items: ReferenceDataItem[]): void {
    http.expectOne(`${BASE}/reference-data/categories`).flush({
      data: [
        { code: 'audit_type', label: 'Audit types' },
        { code: 'exception_category', label: 'Exception categories' },
      ],
    });
    http
      .expectOne((r) => r.url === `${BASE}/reference-data/audit_type`)
      .flush({ data: items });
  }

  afterEach(() => http.verify());

  it('loads categories, selects the first, and renders its items', async () => {
    setup();
    flushInitial([item('aml', 'AML'), item('it', 'IT')]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.categories().length).toBe(2);
    expect(component.selectedCategory()?.code).toBe('audit_type');
    expect(component.items().length).toBe(2);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Audit types');
    expect(text).toContain('AML');
  });

  it('switches list: the heading follows the selection and the new items load', async () => {
    setup();
    flushInitial([item('aml', 'AML')]);
    await fixture.whenStable();

    component.search.set('aml');
    component.selectCategory('exception_category');
    http
      .expectOne((r) => r.url === `${BASE}/reference-data/exception_category`)
      .flush({ data: [item('cash', 'Cash handling', { category: 'exception_category' })] });
    await fixture.whenStable();
    fixture.detectChanges();

    // Regression: the selection used to be read from the form control inside a computed, which is not
    // reactive — the heading and dialog title kept naming the FIRST list after switching.
    expect(component.selectedCategory()?.code).toBe('exception_category');
    expect(component.search()).toBe('');
    const heading = (fixture.nativeElement as HTMLElement).querySelector('.ref__title');
    expect(heading?.textContent).toContain('Exception categories');
    expect(component.visibleItems().map((i) => i.code)).toEqual(['cash']);
  });

  it('ignores a slow response for a list the user has already left', async () => {
    setup();
    flushInitial([item('aml', 'AML')]);
    await fixture.whenStable();

    component.selectCategory('exception_category');
    const stale = http.expectOne((r) => r.url === `${BASE}/reference-data/exception_category`);
    component.selectCategory('audit_type');
    const current = http.expectOne((r) => r.url === `${BASE}/reference-data/audit_type`);

    current.flush({ data: [item('it', 'IT')] });
    stale.flush({ data: [item('cash', 'Cash handling', { category: 'exception_category' })] });

    expect(component.items().map((i) => i.code)).toEqual(['it']);
  });

  it('filters the loaded items by label, code or description without refetching', async () => {
    setup();
    flushInitial([
      item('aml', 'AML'),
      item('it', 'IT general controls'),
      item('ops', 'Operations', { description: 'Branch and back-office controls' }),
    ]);
    await fixture.whenStable();

    component.search.set('CONTROLS');
    expect(component.visibleItems().map((i) => i.code)).toEqual(['it', 'ops']);
    expect(component.summary()).toContain('2 of 3');

    component.search.set('zzz');
    expect(component.noMatches()).toBe(true);
    expect(component.isEmpty()).toBe(false);
    // afterEach's http.verify() proves searching issued no request.
  });

  it('shows how many active items each list holds', async () => {
    setup();
    http.expectOne(`${BASE}/reference-data/categories`).flush({
      data: [
        { code: 'audit_type', label: 'Audit types', description: 'Classifies audits.', activeCount: 8, inactiveCount: 1 },
        { code: 'entity_type', label: 'Entity types', description: 'Classifies entities.', activeCount: 0, inactiveCount: 0 },
      ],
    });
    http.expectOne((r) => r.url === `${BASE}/reference-data/audit_type`).flush({ data: [item('aml', 'AML')] });
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const counts = [...root.querySelectorAll('.ref__rail-count')].map((e) => e.textContent?.trim());
    expect(counts).toEqual(['8', '0']);
    // An empty list is flagged — nothing would appear in its dropdowns.
    expect(root.querySelectorAll('.ref__rail-count--empty').length).toBe(1);
    expect(root.querySelector('.ref__desc')?.textContent).toContain('Classifies audits.');
    expect(component.summary()).toContain('1 archived');
  });

  it('re-queries with includeInactive when the toggle is set', async () => {
    setup();
    flushInitial([item('aml', 'AML')]);
    await fixture.whenStable();
    fixture.detectChanges();

    component.controls.controls.showInactive.setValue(true);
    const req = http.expectOne(
      (r) => r.url === `${BASE}/reference-data/audit_type`,
    );
    expect(req.request.params.get('includeInactive')).toBe('true');
    req.flush({ data: [item('aml', 'AML'), item('old', 'Old', { isActive: false })] });

    expect(component.items().length).toBe(2);
  });

  it('shows the empty state when a category has no items', async () => {
    setup();
    flushInitial([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isEmpty()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No items');
  });

  it('hides the New item button without ManageConfiguration', async () => {
    setup(['ViewConfig']);
    flushInitial([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.canManage()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('New item');
  });
});
