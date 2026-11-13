import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';

import { provideTestEnv } from '../../../../testing/test-providers';
import { EntityAuditHistory, EntityAuditHistoryItem, SessionDto } from '../../../core/models';
import { AuthService } from '../../../core/services/auth.service';
import {
  BulkImportDialogComponent,
  ENTITY_IMPORT_COLUMNS,
  buildEntityImportTemplate,
} from './bulk-import-dialog.component';
import { EntityAuditHistoryDialogComponent } from './entity-audit-history-dialog.component';

const BASE = '/api/v1';

function session(permissions: string[]): SessionDto {
  return {
    userId: 'u-1',
    email: 'manager@bank.test',
    firstName: 'Mary',
    lastName: 'Manager',
    displayName: 'Mary Manager',
    status: 'active',
    roles: [],
    permissions,
    expiresAt: '',
    absoluteExpiresAt: '',
  };
}

function historyItem(overrides: Partial<EntityAuditHistoryItem> = {}): EntityAuditHistoryItem {
  return {
    auditId: 'a-1',
    name: 'Lagos Branch Operations Audit',
    auditType: 'branch',
    status: 'completed',
    isSelfAssessment: false,
    startDate: '2026-05-14',
    targetEndDate: '2026-08-12',
    actualEndDate: '2026-08-10',
    leadUserId: 'u-lead',
    leadName: 'Mary Manager',
    auditeeUserId: 'u-auditee',
    auditeeName: 'Aud Itee',
    origin: 'plan',
    planPeriodLabel: 'FY2026',
    checklistItems: 7,
    respondedItems: 7,
    passed: 5,
    failed: 1,
    notApplicable: 1,
    findings: 4,
    openFindings: 1,
    criticalFindings: 0,
    highFindings: 3,
    mediumFindings: 1,
    lowFindings: 0,
    reportsIssued: 2,
    ...overrides,
  };
}

function history(audits: EntityAuditHistoryItem[], overrides: Partial<EntityAuditHistory> = {}): EntityAuditHistory {
  return {
    entityId: 'e-1',
    entityName: 'Lagos Main Branch',
    entityType: 'branch',
    isArchived: false,
    lastAuditedAt: '2026-08-10T00:00:00Z',
    expectedAuditsPerYear: 1,
    totalAudits: audits.filter((a) => !a.isSelfAssessment).length,
    completedAudits: audits.filter((a) => a.status === 'completed' && !a.isSelfAssessment).length,
    inFlightAudits: 0,
    selfAssessments: audits.filter((a) => a.isSelfAssessment).length,
    totalFindings: audits.reduce((n, a) => n + a.findings, 0),
    openFindings: audits.reduce((n, a) => n + a.openFindings, 0),
    audits,
    ...overrides,
  };
}

describe('buildEntityImportTemplate', () => {
  it('starts with the exact header the importer expects', () => {
    const [header] = buildEntityImportTemplate(['branch', 'process']).split('\r\n');
    expect(header).toBe('name,entity_type,parent_name,owner_email,description');
    expect(header).toBe(ENTITY_IMPORT_COLUMNS.join(','));
  });

  it("uses the bank's own entity types in the examples so they import as they are", () => {
    const lines = buildEntityImportTemplate(['department', 'subsidiary']).trim().split('\r\n');
    const types = lines.slice(1).map((line) => line.split(',')[1]);
    expect(types.every((t) => t === 'department' || t === 'subsidiary')).toBe(true);
    // A parent/child pair, so the parent_name column is shown in use.
    expect(lines.slice(1).some((line) => line.split(',')[2] === 'Head Office')).toBe(true);
  });

  it('falls back to a generic type when none are configured, and every row carries all five columns', () => {
    const lines = buildEntityImportTemplate([]).trim().split('\r\n');
    expect(lines.length).toBe(4);
    for (const line of lines) {
      expect(line.split(',').length).toBe(5);
    }
    expect(lines[1].split(',')[1]).toBe('branch');
  });
});

describe('BulkImportDialogComponent', () => {
  let http: HttpTestingController;
  let dialogRef: jasmine.SpyObj<MatDialogRef<BulkImportDialogComponent, number>>;

  function create() {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    TestBed.configureTestingModule({
      imports: [BulkImportDialogComponent],
      providers: [...provideTestEnv(), { provide: MatDialogRef, useValue: dialogRef }],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(BulkImportDialogComponent);
    fixture.detectChanges();
    http.expectOne(`${BASE}/audit-universe/entity-types`).flush({ data: ['branch', 'process'] });
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  it("lists the bank's active entity types and offers the template and a file chooser", () => {
    const fixture = create();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('branch');
    expect(text).toContain('process');
    expect(fixture.componentInstance.entityTypes()).toEqual(['branch', 'process']);
    expect((fixture.nativeElement as HTMLElement).querySelector('input[type="file"]')).not.toBeNull();
  });

  it('keeps the dialog open and lists the row errors when the import is rejected', () => {
    const fixture = create();
    const component = fixture.componentInstance;
    component.form.controls.csvContent.setValue('name,entity_type\nBad,not_a_type\n');
    component.submit();
    http.expectOne(`${BASE}/audit-universe/entities/bulk-import`).flush({
      data: { created: 0, errors: [{ row: 2, field: 'entity_type', message: "Unknown entity type 'not_a_type'." }] },
    });
    fixture.detectChanges();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(component.errors().length).toBe(1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain("Unknown entity type 'not_a_type'.");
  });

  it('closes with the number created when the whole sheet imports', () => {
    const fixture = create();
    fixture.componentInstance.form.controls.csvContent.setValue('name,entity_type\nHead Office,branch\n');
    fixture.componentInstance.submit();
    http.expectOne(`${BASE}/audit-universe/entities/bulk-import`).flush({ data: { created: 1, errors: [] } });
    expect(dialogRef.close).toHaveBeenCalledWith(1);
  });
});

describe('EntityAuditHistoryDialogComponent', () => {
  let http: HttpTestingController;

  function create(payload: EntityAuditHistory, permissions: string[] = ['ViewUniverse', 'ViewAudit']) {
    TestBed.configureTestingModule({
      imports: [EntityAuditHistoryDialogComponent],
      providers: [
        ...provideTestEnv(),
        provideRouter([]),
        { provide: MAT_DIALOG_DATA, useValue: { entityId: 'e-1', entityName: 'Lagos Main Branch' } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).setSession(session(permissions));
    const fixture = TestBed.createComponent(EntityAuditHistoryDialogComponent);
    fixture.detectChanges();
    http.expectOne(`${BASE}/audit-universe/entities/e-1/audit-history`).flush({ data: payload });
    // Audit-type labels are looked up lazily from reference data.
    http.match((r) => r.url.includes('/reference-data')).forEach((r) => r.flush({ data: [] }));
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  it('shows every audit of the entity with its outcome, and links to the audit', () => {
    const fixture = create(history([
      historyItem(),
      historyItem({ auditId: 'a-2', name: 'Cash handling spot check', origin: 'ad_hoc', planPeriodLabel: null, status: 'in_progress', actualEndDate: null, findings: 0, openFindings: 0, highFindings: 0, mediumFindings: 0 }),
    ]));
    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent ?? '';

    expect(text).toContain('Lagos Branch Operations Audit');
    expect(text).toContain('Cash handling spot check');
    expect(el.querySelectorAll('.hist__audit').length).toBe(2);
    // Where each audit came from.
    expect([...el.querySelectorAll('.hist__chip')].map((c) => c.getAttribute('data-origin'))).toEqual(['plan', 'ad_hoc']);
    // Names link to the audit for someone who can open audits.
    expect(el.querySelector('a.hist__audit-name')?.getAttribute('href')).toBe('/audits/a-1');
  });

  it('breaks findings down by severity, most severe first, leaving out severities with none', () => {
    const fixture = create(history([historyItem()]));
    expect(fixture.componentInstance.severities(historyItem()).map((s) => s.key)).toEqual(['high', 'medium']);
    expect(fixture.componentInstance.severities(historyItem({ criticalFindings: 1, lowFindings: 2 })).map((s) => s.key))
      .toEqual(['critical', 'high', 'medium', 'low']);
  });

  it('says so plainly when no audit has ever been linked to the entity', () => {
    const fixture = create(history([], { lastAuditedAt: null }));
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.hist__list')).toBeNull();
    expect(el.querySelector('.hist__empty')).not.toBeNull();
  });

  it('does not link audit names for a viewer who cannot open audits', () => {
    const fixture = create(history([historyItem()]), ['ViewUniverse']);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a.hist__audit-name')).toBeNull();
    expect(el.querySelector('span.hist__audit-name')?.textContent).toContain('Lagos Branch Operations Audit');
  });
});
