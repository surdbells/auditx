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
