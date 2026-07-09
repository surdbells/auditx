import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { TimeTrackingService } from './time-tracking.service';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('TimeTrackingService', () => {
  let service: TimeTrackingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(TimeTrackingService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists an audit\'s entries', () => {
    service.list('a-1').subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/time-entries`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [] });
  });

  it('fetches the budget-vs-actual summary', () => {
    service.summary('a-1').subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/time-entries/summary`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: { auditId: 'a-1', actualHours: 0, entryCount: 0, byCategory: [], byUser: [] } });
  });

  it('logs time against an audit', () => {
    service.log('a-1', { workDate: '2026-06-20', hours: 4, category: 'fieldwork' }).subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/time-entries`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ workDate: '2026-06-20', hours: 4, category: 'fieldwork' });
    req.flush({ data: {} });
  });

  it('amends a time entry via the flat /time-entries path', () => {
    service.update('t-1', { workDate: '2026-06-20', hours: 5, category: 'review', version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/time-entries/t-1`);
    expect(req.request.method).toBe('PATCH');
    req.flush({ data: {} });
  });

  it('deletes a time entry, URL-encoding the version', () => {
    service.delete('t-1', 'AA+/B==').subscribe();
    const req = http.expectOne(`${BASE}/time-entries/t-1?version=AA%2B%2FB%3D%3D`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('sets the audit budget', () => {
    service.setBudget('a-1', { budgetedHours: 40, version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/budget`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ budgetedHours: 40, version: 'v1' });
    req.flush({ data: {} });
  });
});
