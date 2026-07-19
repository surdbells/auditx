import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { provideTestEnv } from '../../../testing/test-providers';
import { EngagementService } from './engagement.service';

const BASE = '/api/v1';

describe('EngagementService', () => {
  let service: EngagementService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(EngagementService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the board and surfaces the next action', () => {
    let waiting: boolean | undefined;
    service.board().subscribe((rows) => (waiting = rows[0].waitingOnMe));
    const req = http.expectOne((r) => r.url === `${BASE}/engagements`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: [
        {
          auditId: 'a-1', name: 'Lagos branch', auditType: 'branch_operations', status: 'in_progress',
          stage: 'fieldwork', progressPercent: 40, openExceptionCount: 0, version: 'v1', waitingOnMe: true,
          nextActions: [{ code: 'respond_items', label: 'Respond to 4 items', kind: 'route', route: '/audits/a-1/execute', targetState: null, permissionKey: 'RespondItem' }],
        },
      ],
    });
    expect(waiting).toBe(true);
  });

  it('fetches a single engagement journey', () => {
    let stage: string | undefined;
    service.journey('a-1').subscribe((j) => (stage = j.stage));
    const req = http.expectOne(`${BASE}/engagements/a-1/journey`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: {
        auditId: 'a-1', name: 'Lagos branch', auditType: 'branch_operations', status: 'under_review',
        stage: 'review', progressPercent: 100, openExceptionCount: 0, version: 'v1',
        stages: [{ code: 'review', label: 'Under review', state: 'current' }], nextActions: [],
      },
    });
    expect(stage).toBe('review');
  });
});
