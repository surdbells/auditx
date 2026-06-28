import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { WebhooksService } from './webhooks.service';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('WebhooksService', () => {
  let service: WebhooksService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(WebhooksService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists subscriptions', () => {
    let result: unknown[] | undefined;
    service.listSubscriptions().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/webhook-subscriptions`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: [
        {
          id: 's-1',
          destinationUrl: 'https://internal/hook',
          subscribedEventTypes: ['audit.created'],
          isActive: true,
        },
      ],
    });
    expect(result?.length).toBe(1);
  });

  it('creates a subscription with hmac secret and retry policy', () => {
    service
      .createSubscription({
        destinationUrl: 'https://internal/hook',
        subscribedEventTypes: ['audit.created'],
        hmacSecret: 'shh',
        retryPolicyJson: '{"max":5}',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/webhook-subscriptions`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.hmacSecret).toBe('shh');
    expect(req.request.body.subscribedEventTypes).toEqual(['audit.created']);
    req.flush({
      data: {
        id: 's-2',
        destinationUrl: 'https://internal/hook',
        subscribedEventTypes: ['audit.created'],
        isActive: true,
      },
    });
  });

  it('deletes a subscription', () => {
    let done = false;
    service.deleteSubscription('s-1').subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/webhook-subscriptions/s-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('lists deliveries with filters, cursor + limit and unwraps the page', () => {
    let result: { items: unknown[]; hasMore: boolean } | undefined;
    service
      .listDeliveries({
        subscriptionId: 's-1',
        status: 'dead_letter',
        cursor: 'cur-1',
        limit: 50,
      })
      .subscribe((page) => (result = page));
    const req = http.expectOne((r) => r.url === `${BASE}/webhook-deliveries`);
    expect(req.request.params.get('subscriptionId')).toBe('s-1');
    expect(req.request.params.get('status')).toBe('dead_letter');
    expect(req.request.params.get('cursor')).toBe('cur-1');
    expect(req.request.params.get('limit')).toBe('50');
    req.flush({
      data: {
        items: [
          {
            id: 'd-1',
            subscriptionId: 's-1',
            eventType: 'audit.created',
            eventId: 'e-1',
            status: 'dead_letter',
            attempts: 5,
            nextRetryAt: null,
            lastError: null,
            deliveredAt: null,
            createdAt: '',
          },
        ],
        nextCursor: 'cur-2',
        hasMore: true,
      },
    });
    expect(result?.items.length).toBe(1);
    expect(result?.hasMore).toBe(true);
  });

  it('retries a delivery via void POST', () => {
    let done = false;
    service.retryDelivery('d-1').subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/webhook-deliveries/d-1/retry`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });
});
