import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { WebhookDeliveriesComponent } from './webhook-deliveries.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { AuthService } from '../../../../core/services/auth.service';
import { SessionDto, WebhookDelivery } from '../../../../core/models';

const BASE = '/api/v1';

function delivery(overrides: Partial<WebhookDelivery> = {}): WebhookDelivery {
  return {
    id: 'd-1',
    subscriptionId: 's-1',
    eventType: 'audit.created',
    eventId: 'e-1',
    status: 'dead_letter',
    attempts: 5,
    nextRetryAt: null,
    lastError: 'connection refused',
    deliveredAt: null,
    createdAt: '',
    ...overrides,
  };
}

function page(
  items: WebhookDelivery[],
  nextCursor: string | null = null,
  hasMore = false,
) {
  return { data: { items, nextCursor, hasMore } };
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

describe('WebhookDeliveriesComponent', () => {
  let fixture: ComponentFixture<WebhookDeliveriesComponent>;
  let component: WebhookDeliveriesComponent;
  let http: HttpTestingController;

  function setup(
    permissions: string[] = ['ConfigureWebhooks', 'AdminOps'],
  ): void {
    TestBed.configureTestingModule({
      imports: [WebhookDeliveriesComponent],
      providers: [provideTestEnv()],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(permissions));

    fixture = TestBed.createComponent(WebhookDeliveriesComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads deliveries (cursor page) and renders them', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/webhook-deliveries`)
      .flush(page([delivery()]));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.deliveries().length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('audit.created');
    // snake_case status is humanised for display.
    expect(text).toContain('Dead letter');
  });

  it('re-fetches with a status query when the filter changes', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/webhook-deliveries`)
      .flush(page([]));
    await fixture.whenStable();

    component.statusFilter.setValue('dead_letter');
    const req = http.expectOne((r) => r.url === `${BASE}/webhook-deliveries`);
    expect(req.request.params.get('status')).toBe('dead_letter');
    req.flush(page([delivery()]));
  });

  it('loads more via the next cursor and appends the page', async () => {
    setup();
    const first = http.expectOne((r) => r.url === `${BASE}/webhook-deliveries`);
    expect(first.request.params.has('cursor')).toBe(false);
    first.flush(page([delivery()], 'cur-2', true));
    await fixture.whenStable();

    expect(component.hasMore()).toBe(true);
    component.loadMore();
    const next = http.expectOne((r) => r.url === `${BASE}/webhook-deliveries`);
    expect(next.request.params.get('cursor')).toBe('cur-2');
    next.flush(page([delivery({ id: 'd-2' })], null, false));

    expect(component.deliveries().length).toBe(2);
    expect(component.hasMore()).toBe(false);
  });

  it('retries a dead-lettered delivery via AdminOps', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/webhook-deliveries`)
      .flush(page([delivery()]));
    await fixture.whenStable();

    expect(component.canRetryRow(delivery())).toBe(true);
    component.retry(delivery());
    const req = http.expectOne(`${BASE}/webhook-deliveries/d-1/retry`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });

    // The component re-fetches after a successful retry.
    http
      .expectOne((r) => r.url === `${BASE}/webhook-deliveries`)
      .flush(page([delivery({ status: 'pending' })]));
  });

  it('does not allow retry without AdminOps', async () => {
    setup(['ConfigureWebhooks']);
    http
      .expectOne((r) => r.url === `${BASE}/webhook-deliveries`)
      .flush(page([delivery()]));
    await fixture.whenStable();

    expect(component.canRetry()).toBe(false);
    expect(component.canRetryRow(delivery())).toBe(false);
  });
});
