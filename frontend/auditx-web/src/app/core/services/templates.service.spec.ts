import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { TemplatesService } from './templates.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { Template } from '../models';

const BASE = '/api/v1';

function template(overrides: Partial<Template> = {}): Template {
  return {
    id: 't-1',
    name: 'AML Review',
    auditType: 'AML',
    description: '',
    status: 'draft',
    currentVersion: 0,
    clonedFromTemplateId: null,
    items: [],
    sections: [],
    versions: [],
    ...overrides,
  };
}

describe('TemplatesService', () => {
  let service: TemplatesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTestEnv()],
    });
    service = TestBed.inject(TemplatesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists templates with query params and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .list({ auditType: 'AML', status: 'all', search: 'x', page: 1, pageSize: 25 })
      .subscribe((page) => (result = page));

    const req = http.expectOne((r) => r.url === `${BASE}/templates`);
    expect(req.request.params.get('auditType')).toBe('AML');
    expect(req.request.params.get('status')).toBe('all');
    expect(req.request.params.get('search')).toBe('x');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: { items: [template()], total: 1, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });

    expect(result?.items.length).toBe(1);
  });

  it('gets a single template and unwraps data', () => {
    let result: Template | undefined;
    service.getById('t-1').subscribe((t) => (result = t));
    http.expectOne(`${BASE}/templates/t-1`).flush({ data: template() });
    expect(result?.id).toBe('t-1');
  });

  it('adds an item and returns the updated template', () => {
    let result: Template | undefined;
    service
      .addItem('t-1', {
        prompt: 'Check KYC',
        referenceNotes: '',
        responseType: 'pass_fail_na',
        sectionName: '',
        isRequired: true,
        defaultAssignmentRuleJson: null,
      })
      .subscribe((t) => (result = t));

    const req = http.expectOne(`${BASE}/templates/t-1/items`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.prompt).toBe('Check KYC');
    req.flush({ data: template({ currentVersion: 0 }) });
    expect(result?.id).toBe('t-1');
  });

  it('reorders items via a void POST', () => {
    let done = false;
    service
      .reorderItems('t-1', { orderedItemIds: ['a', 'b'] })
      .subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/templates/t-1/items/reorder`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('removes a section with the name as a query param', () => {
    let done = false;
    service.removeSection('t-1', 'Section A').subscribe(() => (done = true));
    const req = http.expectOne(
      (r) => r.method === 'DELETE' && r.url.startsWith(`${BASE}/templates/t-1/sections`),
    );
    expect(req.request.url).toContain('name=Section%20A');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  describe('publish', () => {
    it('returns a published result on a 200 response', () => {
      let result: { kind: string } | undefined;
      service.publish('t-1').subscribe((r) => (result = r));

      const req = http.expectOne(`${BASE}/templates/t-1/publish`);
      expect(req.request.method).toBe('POST');
      req.flush(
        { data: template({ status: 'published', currentVersion: 1 }) },
        { status: 200, statusText: 'OK' },
      );

      expect(result?.kind).toBe('published');
      if (result?.kind === 'published') {
        // type narrowing path covered
      }
    });

    it('returns a pending result on a 202 response', () => {
      let result:
        | { kind: 'published'; template: Template }
        | { kind: 'pending'; pendingActionId: string }
        | undefined;
      service.publish('t-1').subscribe((r) => (result = r));

      http
        .expectOne(`${BASE}/templates/t-1/publish`)
        .flush(
          { data: { pendingActionId: 'pa-99' } },
          { status: 202, statusText: 'Accepted' },
        );

      expect(result?.kind).toBe('pending');
      if (result?.kind === 'pending') {
        expect(result.pendingActionId).toBe('pa-99');
      }
    });
  });

  it('clones a template', () => {
    let result: Template | undefined;
    service.clone('t-1', { newName: 'Copy' }).subscribe((t) => (result = t));
    const req = http.expectOne(`${BASE}/templates/t-1/clone`);
    expect(req.request.body.newName).toBe('Copy');
    req.flush({ data: template({ id: 't-2', name: 'Copy' }) });
    expect(result?.id).toBe('t-2');
  });

  it('fetches a version diff', () => {
    service.diff('t-1', 1, 2).subscribe();
    const req = http.expectOne(`${BASE}/templates/t-1/versions/1/diff/2`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: { fromVersion: 1, toVersion: 2, added: [], removed: [], modified: [] },
    });
  });
});
