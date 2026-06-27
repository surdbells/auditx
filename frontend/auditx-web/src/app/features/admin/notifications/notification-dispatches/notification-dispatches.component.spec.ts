import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { NotificationDispatchesComponent } from './notification-dispatches.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { NotificationDispatch } from '../../../../core/models';

const BASE = '/api/v1';

function dispatch(
  overrides: Partial<NotificationDispatch> = {},
): NotificationDispatch {
  return {
    id: 'd-1',
    eventId: 'e-1',
    eventType: 'audit.created',
    ruleId: 'r-1',
    recipientUserId: 'u-1',
    recipientAddress: 'cae@bank.test',
    channel: 'email',
    templateKey: 'audit.created.email',
    templateVersion: 1,
    renderedSubject: 'New audit',
    severity: null,
    status: 'dead_letter',
    attempts: 5,
    nextRetryAt: null,
    deliveredAt: null,
    lastError: 'smtp timeout',
    ...overrides,
  };
}

function page(items: NotificationDispatch[]) {
  return { data: { items, nextCursor: null, hasMore: false } };
}

describe('NotificationDispatchesComponent', () => {
  let fixture: ComponentFixture<NotificationDispatchesComponent>;
  let component: NotificationDispatchesComponent;
  let http: HttpTestingController;

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [NotificationDispatchesComponent],
      providers: [provideTestEnv()],
    });
    fixture = TestBed.createComponent(NotificationDispatchesComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads dispatches and renders them with a humanised status', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/notification-dispatches`)
      .flush(page([dispatch()]));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.dispatches().length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('audit.created');
    expect(text).toContain('cae@bank.test');
    // snake_case status is humanised for display.
    expect(text).toContain('Dead letter');
  });

  it('re-fetches with a status query when the filter changes', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/notification-dispatches`)
      .flush(page([]));
    await fixture.whenStable();

    component.statusFilter.setValue('dead_letter');
    const req = http.expectOne(
      (r) => r.url === `${BASE}/notification-dispatches`,
    );
    expect(req.request.params.get('status')).toBe('dead_letter');
    req.flush(page([dispatch()]));
  });

  it('passes the event-type filter on the next fetch', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/notification-dispatches`)
      .flush(page([]));
    await fixture.whenStable();

    component.eventTypeFilter.setValue('audit.created');
    component.fetch();
    const req = http.expectOne(
      (r) => r.url === `${BASE}/notification-dispatches`,
    );
    expect(req.request.params.get('eventType')).toBe('audit.created');
    req.flush(page([dispatch()]));
  });

  it('humanises dispatch statuses', () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/notification-dispatches`)
      .flush(page([]));
    expect(component.humanise('dead_letter')).toBe('Dead letter');
    expect(component.humanise('delivered')).toBe('Delivered');
    expect(component.humanise(null)).toBe('—');
  });
});
