import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { MyActionsComponent } from './my-actions.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { MyActionItem } from '../../../core/models';

const BASE = '/api/v1';

function item(overrides: Partial<MyActionItem> = {}): MyActionItem {
  return {
    kind: 'complete_action',
    exceptionId: 'e-1',
    exceptionTitle: 'Payments released without dual control',
    severity: 'high',
    exceptionStatus: 'map_approved',
    auditId: 'a-1',
    actionId: 'act-1',
    extensionRequestId: null,
    description: 'Enforce maker-checker in the core system',
    dueDate: '2027-01-20',
    daysUntilDue: 5,
    isOverdue: false,
    extensionPending: false,
    ...overrides,
  };
}

describe('MyActionsComponent', () => {
  let fixture: ComponentFixture<MyActionsComponent>;
  let component: MyActionsComponent;
  let http: HttpTestingController;

  function setup(rows: MyActionItem[]): void {
    TestBed.configureTestingModule({
      imports: [MyActionsComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(MyActionsComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne((r) => r.url === `${BASE}/my/actions`).flush({ data: rows });
    fixture.detectChanges();
  }

  const root = () => fixture.nativeElement as HTMLElement;

  afterEach(() => http.verify());

  it('shows an empty state when nothing waits on me', () => {
    setup([]);
    expect(root().querySelector('app-empty-state')).not.toBeNull();
    expect(root().querySelectorAll('.item').length).toBe(0);
  });

  it('lists each item linking to its finding, with the overdue ones flagged and counted', () => {
    setup([
      item({ isOverdue: true, daysUntilDue: -3, dueDate: '2027-01-12' }),
      item({ kind: 'decide_extension', actionId: null, extensionRequestId: 'r-1', exceptionId: 'e-2' }),
      item({ kind: 'submit_plan', actionId: null, exceptionId: 'e-3', extensionPending: true, daysUntilDue: 30 }),
    ]);

    const cards = root().querySelectorAll('.item');
    expect(cards.length).toBe(3);
    expect(cards[0].classList).toContain('item--overdue');
    expect(component.overdueCount()).toBe(1);
    expect(component.dueSoonCount()).toBe(1);
    expect(cards[0].querySelector('a.item__title')?.getAttribute('href')).toBe('/exceptions/e-1');
    expect(cards[2].querySelector('.status-badge')).not.toBeNull();
  });

  it('shows an error with a retry when the list cannot load', () => {
    TestBed.configureTestingModule({ imports: [MyActionsComponent], providers: [provideTestEnv(), provideRouter([])] });
    fixture = TestBed.createComponent(MyActionsComponent);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne(`${BASE}/my/actions`).flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(root().querySelector('app-error-state')).not.toBeNull();
  });
});
