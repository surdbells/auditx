import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { NotificationAdminService } from './notifications-admin.service';
import { provideTestEnv } from '../../../testing/test-providers';
import {
  NotificationDispatch,
  NotificationRule,
  NotificationTemplate,
} from '../models';

const BASE = '/api/v1';

function rule(overrides: Partial<NotificationRule> = {}): NotificationRule {
  return {
    id: 'r-1',
    eventType: 'audit.created',
    name: 'Audit created → CAE',
    recipientResolutionJson: '{"type":"role","value":"ChiefAuditExecutive"}',
    channelsJson: '["email"]',
    templateKey: 'audit.created.email',
    isActive: true,
    isSystemDefault: false,
    version: 'AAAAAAAAB9E=',
    ...overrides,
  };
}

function template(
  overrides: Partial<NotificationTemplate> = {},
): NotificationTemplate {
  return {
    id: 't-1',
    templateKey: 'audit.created.email',
    channel: 'email',
    scope: 'system',
    subjectTemplate: 'New audit',
    bodyTemplate: 'An audit was created.',
    version: 1,
    ...overrides,
  };
}

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

describe('NotificationAdminService', () => {
  let service: NotificationAdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(NotificationAdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('fetches the event catalogue', () => {
    let result: string[] | undefined;
    service.eventCatalogue().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/admin/events/catalogue`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: ['audit.created', 'audit.completed'] });
    expect(result?.length).toBe(2);
  });

  it('lists rules and unwraps data', () => {
    let result: NotificationRule[] | undefined;
    service.listRules().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/notification-rules`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [rule()] });
    expect(result?.[0].id).toBe('r-1');
  });

  it('creates a rule', () => {
    service
      .createRule({
        eventType: 'audit.created',
        name: 'n',
        recipientResolutionJson: '{}',
        channelsJson: '["email"]',
        templateKey: 'k',
        isActive: true,
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/notification-rules`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.eventType).toBe('audit.created');
    req.flush({ data: rule() });
  });

  it('updates a rule carrying the version for optimistic concurrency', () => {
    service
      .updateRule('r-1', {
        name: 'renamed',
        recipientResolutionJson: '{}',
        channelsJson: '["email"]',
        templateKey: 'k',
        isActive: true,
        version: 'AAAAAAAAB9E=',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/notification-rules/r-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.version).toBe('AAAAAAAAB9E=');
    req.flush({ data: rule({ name: 'renamed' }) });
  });

  it('deactivates a rule via a void DELETE', () => {
    let done = false;
    service.deactivateRule('r-1').subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/notification-rules/r-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('previews a rule', () => {
    service
      .previewRule({
        recipientResolutionJson: '{}',
        templateKey: 'k',
        samplePayloadJson: '{}',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/notification-rules/preview`);
    expect(req.request.method).toBe('POST');
    req.flush({
      data: {
        resolvedRecipientAddresses: ['a@b.c'],
        renderedSubject: 'Subj',
        renderedBody: 'Body',
      },
    });
  });

  it('lists templates', () => {
    let result: NotificationTemplate[] | undefined;
    service.listTemplates().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/notification-templates`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [template()] });
    expect(result?.[0].scope).toBe('system');
  });

  it('posts a bank-scope template override', () => {
    service
      .overrideTemplate({
        templateKey: 'audit.created.email',
        channel: 'email',
        subjectTemplate: 'Override',
        bodyTemplate: 'Body',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/notification-templates`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.templateKey).toBe('audit.created.email');
    req.flush({ data: template({ scope: 'bank' }) });
  });

  it('lists dispatches with status and event-type filters and unwraps the page', () => {
    let items: NotificationDispatch[] | undefined;
    service
      .listDispatches({
        status: 'dead_letter',
        eventType: 'audit.created',
        page: 1,
        pageSize: 25,
      })
      .subscribe((result) => (items = result.items));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/notification-dispatches`,
    );
    expect(req.request.params.get('status')).toBe('dead_letter');
    expect(req.request.params.get('eventType')).toBe('audit.created');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({
      data: {
        items: [dispatch()],
        total: 1,
        page: 1,
        pageSize: 25,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      },
    });
    expect(items?.length).toBe(1);
  });

  it('lists the dead-letter queue', () => {
    service.listDeadLetter(1, 25).subscribe();
    const req = http.expectOne(
      (r) => r.url === `${BASE}/notification-dispatches/dead-letter`,
    );
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({
      data: {
        items: [],
        total: 0,
        page: 1,
        pageSize: 25,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      },
    });
  });

  it('retries a dispatch via POST', () => {
    let result: NotificationDispatch | undefined;
    service.retryDispatch('d-1').subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/notification-dispatches/d-1/retry`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: dispatch({ status: 'pending' }) });
    expect(result?.status).toBe('pending');
  });

  it('gets the current user preferences', () => {
    service.getMyPreferences().subscribe();
    const req = http.expectOne(`${BASE}/users/me/notification-preferences`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: { userId: 'u-1', preferencesJson: null } });
  });

  it('updates the current user preferences via a void PATCH', () => {
    let done = false;
    service
      .updateMyPreferences({ preferencesJson: '{"digests":true}' })
      .subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/users/me/notification-preferences`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.preferencesJson).toBe('{"digests":true}');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });
});
