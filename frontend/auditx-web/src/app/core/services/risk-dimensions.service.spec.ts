import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { RiskDimensionsService } from './risk-dimensions.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { RiskDimension } from '../models';

const BASE = '/api/v1';

function dimension(overrides: Partial<RiskDimension> = {}): RiskDimension {
  return {
    id: 'd-1',
    name: 'Financial',
    weight: 1,
    scaleMin: 1,
    scaleMax: 5,
    isActive: true,
    scaleLabelOverridesJson: null,
    ...overrides,
  };
}

describe('RiskDimensionsService', () => {
  let service: RiskDimensionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(RiskDimensionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists active dimensions by default', () => {
    let result: RiskDimension[] | undefined;
    service.list().subscribe((d) => (result = d));
    const req = http.expectOne((r) => r.url === `${BASE}/risk-dimensions`);
    expect(req.request.params.get('active')).toBe('true');
    req.flush({ data: [dimension()] });
    expect(result?.length).toBe(1);
  });

  it('lists all dimensions when asked', () => {
    service.list('all').subscribe();
    const req = http.expectOne((r) => r.url === `${BASE}/risk-dimensions`);
    expect(req.request.params.get('active')).toBe('all');
    req.flush({ data: [] });
  });

  it('creates a dimension', () => {
    let result: RiskDimension | undefined;
    service
      .create({ name: 'Reputational', weight: 2, scaleMin: 1, scaleMax: 5 })
      .subscribe((d) => (result = d));
    const req = http.expectOne(`${BASE}/risk-dimensions`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.name).toBe('Reputational');
    req.flush({ data: dimension({ id: 'd-2', name: 'Reputational' }) });
    expect(result?.id).toBe('d-2');
  });

  it('updates a dimension with a PATCH', () => {
    service.update('d-1', { weight: 3, isActive: false }).subscribe();
    const req = http.expectOne(`${BASE}/risk-dimensions/d-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.weight).toBe(3);
    expect(req.request.body.isActive).toBe(false);
    req.flush({ data: dimension({ weight: 3, isActive: false }) });
  });
});
