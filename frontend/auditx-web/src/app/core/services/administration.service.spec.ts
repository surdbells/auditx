import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AdministrationService } from './administration.service';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('AdministrationService', () => {
  let service: AdministrationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(AdministrationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets bank settings', () => {
    let name: string | undefined;
    service.getBankSettings().subscribe((s) => (name = s.bankDisplayName));
    http.expectOne(`${BASE}/admin/bank-settings`).flush({
      data: {
        bankDisplayName: 'ACME Bank',
        timezone: 'UTC',
        localeDefault: 'en',
        adProvisioningFilterOuDn: null,
        adProvisioningFilterGroupSid: null,
        maxEvidenceFileMb: 25,
        maxAuditEvidenceGb: 10,
      },
    });
    expect(name).toBe('ACME Bank');
  });

  it('patches bank settings', () => {
    service
      .updateBankSettings({
        bankDisplayName: 'New',
        timezone: 'UTC',
        localeDefault: 'en',
        adProvisioningFilterOuDn: null,
        adProvisioningFilterGroupSid: null,
        allowOverlappingPlanPeriods: false,
        allowAuditLaunchBeforeApproval: false,
        primaryColor: '#4f46e5',
        accentColor: '#7c3aed',
        logoDataUri: null,
        iconDataUri: null,
        showOverview: true,
        showWalkthrough: true,
        reportRetentionMonths: 0,
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/admin/bank-settings`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.bankDisplayName).toBe('New');
    req.flush({ data: { bankDisplayName: 'New' } });
  });

  it('patches resource limits', () => {
    service
      .updateResourceLimits({ maxEvidenceFileMb: 50, maxAuditEvidenceGb: 20 })
      .subscribe();
    const req = http.expectOne(`${BASE}/admin/resource-limits`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.maxEvidenceFileMb).toBe(50);
    req.flush({ data: { maxEvidenceFileMb: 50, maxAuditEvidenceGb: 20 } });
  });

  it('bulk-deactivates users and returns a result', () => {
    let result: { successCount: number } | undefined;
    service.bulkDeactivateUsers(['u1', 'u2']).subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/admin/users/bulk-deactivate`);
    expect(req.request.body.userIds).toEqual(['u1', 'u2']);
    req.flush({ data: { successCount: 2, errors: [] } });
    expect(result?.successCount).toBe(2);
  });

  it('bulk-imports users from csv content', () => {
    service.bulkImportUsers('a,b,c').subscribe();
    const req = http.expectOne(`${BASE}/admin/users/bulk-import`);
    expect(req.request.body.csvContent).toBe('a,b,c');
    req.flush({ data: { successCount: 1, errors: [] } });
  });

  it('enables the support channel', () => {
    service
      .enableSupportChannel({
        engineerIdentifiers: ['eng@x'],
        durationMinutes: 60,
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/admin/support-channel/enable`);
    expect(req.request.body.durationMinutes).toBe(60);
    req.flush({
      data: {
        active: true,
        engineerIdentifiers: ['eng@x'],
        enabledAt: null,
        expiresAt: null,
        revokedAt: null,
      },
    });
  });

  it('revokes the support channel via void POST', () => {
    let done = false;
    service.revokeSupportChannel().subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/admin/support-channel/revoke`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('installs a release', () => {
    service
      .installRelease({
        version: '1.0.0',
        manifestSha256: 'abc',
        changeRecordReference: 'CR-1',
        signatureBase64: 'sig',
        manifestContentBase64: 'man',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/admin/releases/install`);
    expect(req.request.body.version).toBe('1.0.0');
    req.flush({
      data: {
        id: 'r-1',
        version: '1.0.0',
        manifestSha256: 'abc',
        changeRecordReference: 'CR-1',
        status: 'Verified',
        detail: null,
        createdAt: '',
      },
    });
  });

  it('records a restore drill', () => {
    service
      .createRestoreDrill({ outcome: 'Success', details: 'ok' })
      .subscribe();
    const req = http.expectOne(`${BASE}/admin/restore-drills`);
    expect(req.request.body.outcome).toBe('Success');
    req.flush({
      data: { id: 'd-1', executedAt: '', outcome: 'Success', details: 'ok' },
    });
  });

  it('requests an object restore', () => {
    service
      .requestObjectRestore({
        objectType: 'Audit',
        objectId: 'a-1',
        snapshotDate: '2026-01-01',
        justification: 'recovery',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/admin/restore/object`);
    expect(req.request.body.objectType).toBe('Audit');
    req.flush({
      data: {
        id: 'o-1',
        objectType: 'Audit',
        objectId: 'a-1',
        snapshotDate: '2026-01-01',
        justification: 'recovery',
        status: 'Requested',
        requestedByUserId: 'u1',
        decidedByUserId: null,
        decisionComment: null,
      },
    });
  });

  it('decides an object restore', () => {
    service
      .decideObjectRestore('o-1', { approve: true, comment: 'go' })
      .subscribe();
    const req = http.expectOne(`${BASE}/admin/restore/object/o-1/decide`);
    expect(req.request.body.approve).toBe(true);
    req.flush({
      data: {
        id: 'o-1',
        objectType: 'Audit',
        objectId: 'a-1',
        snapshotDate: '2026-01-01',
        justification: 'recovery',
        status: 'Approved',
        requestedByUserId: 'u1',
        decidedByUserId: 'u2',
        decisionComment: 'go',
      },
    });
  });

  it('gets system health metrics', () => {
    let count: number | undefined;
    service.getSystemHealth().subscribe((h) => (count = h.totalUserCount));
    http.expectOne(`${BASE}/admin/system-health`).flush({
      data: {
        status: 'Healthy',
        activeUserCount: 5,
        totalUserCount: 8,
        templateCount: 3,
        integrationCount: 2,
        generatedAt: '',
      },
    });
    expect(count).toBe(8);
  });
});
