import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { SearchService } from './search.service';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('SearchService', () => {
  let service: SearchService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(SearchService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('queries /search with the term and unwraps the hits', () => {
    let result: { hits: unknown[] } | undefined;
    service.search('audit').subscribe((r) => (result = r));
    const req = http.expectOne((r) => r.url === `${BASE}/search`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('q')).toBe('audit');
    req.flush({
      data: { hits: [{ type: 'audit', id: 'a1', title: 'Q1 Audit', subtitle: 'Draft' }] },
    });
    expect(result?.hits.length).toBe(1);
  });
});
