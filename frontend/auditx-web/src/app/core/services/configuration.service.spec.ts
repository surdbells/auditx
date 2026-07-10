import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { ConfigurationService } from './configuration.service';
import { provideTestEnv } from '../../../testing/test-providers';
import {
  ConfigurationActionResult,
  ConfigurationVersion,
  ExceptionDefaultsDefinition,
} from '../models';

const BASE = '/api/v1';
const DOMAIN = 'exception_defaults';

const DEFINITION_JSON = JSON.stringify({
  target_days: { critical: 30, high: 60, medium: 90, low: 120 },
  recurrence_window_months: 12,
  recurrence_threshold: 3,
});

function version(
  overrides: Partial<ConfigurationVersion> = {},
): ConfigurationVersion {
  return {
    id: 'cv-1',
    domain: DOMAIN,
    versionNumber: 1,
    definitionJson: DEFINITION_JSON,
    isActive: true,
    changeReason: 'Initial seed of the defaults.',
    createdByUserId: 'u-1',
    createdAtUtc: '2026-06-01T10:00:00Z',
    activatedBy: 'u-1',
    activatedAt: '2026-06-01T10:05:00Z',
    version: 'rv1',
    ...overrides,
  };
}

describe('ConfigurationService', () => {
  let service: ConfigurationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(ConfigurationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('fetches the active version and unwraps the envelope', () => {
    let result: ConfigurationVersion | undefined;
    service.getActive(DOMAIN).subscribe((v) => (result = v));
    const req = http.expectOne(`${BASE}/configurations/${DOMAIN}`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: version() });
    expect(result?.versionNumber).toBe(1);
  });

  it('lists versions with page/pageSize params and unwraps the page', () => {
    let result: { items: ConfigurationVersion[] } | undefined;
    service.listVersions(DOMAIN, 2, 25).subscribe((page) => (result = page));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/configurations/${DOMAIN}/versions`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({
      data: {
        items: [version()],
        total: 1,
        page: 2,
        pageSize: 25,
        totalPages: 1,
        hasPrevious: true,
        hasNext: false,
      },
    });
    expect(result?.items.length).toBe(1);
  });

  it('creates a draft via POST and unwraps the new version', () => {
    let result: ConfigurationVersion | undefined;
    service
      .createDraft(DOMAIN, {
        definitionJson: DEFINITION_JSON,
        changeReason: 'Tightening the critical SLA window.',
      })
      .subscribe((v) => (result = v));
    const req = http.expectOne(`${BASE}/configurations/${DOMAIN}`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.definitionJson).toBe(DEFINITION_JSON);
    expect(req.request.body.changeReason).toContain('Tightening');
    req.flush({ data: version({ versionNumber: 2, isActive: false }) });
    expect(result?.versionNumber).toBe(2);
    expect(result?.isActive).toBe(false);
  });

  it('activates a version and returns the version on 200', () => {
    let result: ConfigurationActionResult | undefined;
    service
      .activateVersion(DOMAIN, 2, {
        changeReason: 'Approved by the audit committee chair.',
      })
      .subscribe((r) => (result = r));
    const req = http.expectOne(
      `${BASE}/configurations/${DOMAIN}/versions/2/activate`,
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body.changeReason).toContain('Approved');
    req.flush({ data: version({ versionNumber: 2 }) });
    expect(result?.version?.versionNumber).toBe(2);
    expect(result?.pendingActionId).toBeUndefined();
  });

  it('activates a version and surfaces the 202 pendingActionId', () => {
    let result: ConfigurationActionResult | undefined;
    service
      .activateVersion(DOMAIN, 2, {
        changeReason: 'Approved by the audit committee chair.',
      })
      .subscribe((r) => (result = r));
    const req = http.expectOne(
      `${BASE}/configurations/${DOMAIN}/versions/2/activate`,
    );
    req.flush(
      { data: { pendingActionId: 'pa-1' } },
      { status: 202, statusText: 'Accepted' },
    );
    expect(result?.pendingActionId).toBe('pa-1');
    expect(result?.version).toBeUndefined();
  });

  it('rolls back and surfaces the 202 pendingActionId', () => {
    let result: ConfigurationActionResult | undefined;
    service
      .rollback(DOMAIN, {
        toVersionNumber: 1,
        changeReason: 'Reverting an erroneous change to the SLAs.',
      })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/configurations/${DOMAIN}/rollback`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.toVersionNumber).toBe(1);
    req.flush(
      { data: { pendingActionId: 'pa-2' } },
      { status: 202, statusText: 'Accepted' },
    );
    expect(result?.pendingActionId).toBe('pa-2');
  });

  it('rolls back directly and returns the version on 200', () => {
    let result: ConfigurationActionResult | undefined;
    service
      .rollback(DOMAIN, {
        toVersionNumber: 1,
        changeReason: 'Reverting an erroneous change to the SLAs.',
      })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/configurations/${DOMAIN}/rollback`);
    req.flush({ data: version({ versionNumber: 1 }) });
    expect(result?.version?.versionNumber).toBe(1);
    expect(result?.pendingActionId).toBeUndefined();
  });

  it('parses and serialises the exception_defaults definition round-trip', () => {
    const parsed = service.parseExceptionDefaults(DEFINITION_JSON);
    const expected: ExceptionDefaultsDefinition = {
      criticalTargetDays: 30,
      highTargetDays: 60,
      mediumTargetDays: 90,
      lowTargetDays: 120,
      recurrenceWindowMonths: 12,
      recurrenceThreshold: 3,
    };
    expect(parsed).toEqual(expected);

    const json = service.serialiseExceptionDefaults(expected);
    expect(JSON.parse(json)).toEqual({
      target_days: { critical: 30, high: 60, medium: 90, low: 120 },
      recurrence_window_months: 12,
      recurrence_threshold: 3,
    });
  });

  it('returns null when the definition JSON is malformed', () => {
    expect(service.parseExceptionDefaults('not json')).toBeNull();
    expect(service.parseExceptionDefaults('{}')).toBeNull();
  });
});
