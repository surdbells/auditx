import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';

import { EngagementRecordsStore } from './engagement-records.store';
import { provideTestEnv } from '../../../../testing/test-providers';
import { NotificationService } from '../../../core/services/notification.service';
import { Audit, EngagementRecords } from '../../../core/models';

const BASE = '/api/v1';

function audit(): Audit {
  return {
    id: 'a-1', name: 'Treasury', scopeDescription: null, auditType: 'operational', status: 'planned',
    startDate: '2027-01-10', targetEndDate: '2027-02-10', actualEndDate: null, templateId: null, templateVersion: null,
    planItemId: null, leadUserId: 'u-lead', auditeeUserId: 'u-auditee', cancellationReason: null, budgetedHours: null,
    isSelfAssessment: false, kickoffScheduledAtUtc: null, kickoffLocation: null, kickoffAgenda: null, version: 'av1',
    teamMembers: [], sections: [], checklistItems: [],
  };
}

function records(overrides: Partial<EngagementRecords> = {}): EngagementRecords {
  return {
    auditId: 'a-1', isSelfAssessment: false, canManage: true, isAuditee: false, canAcknowledge: false,
    letter: null,
    independence: { required: true, complete: false, outstanding: 1, canDeclare: true, canDecide: true, members: [] },
    meetings: [], draftReports: [], auditVersion: 'av1',
    ...overrides,
  };
}

describe('EngagementRecordsStore', () => {
  let store: EngagementRecordsStore;
  let http: HttpTestingController;
  let dialog: jasmine.SpyObj<MatDialog>;
  let notify: jasmine.SpyObj<NotificationService>;

  beforeEach(() => {
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'info', 'warning', 'error']);
    TestBed.configureTestingModule({
      providers: [
        provideTestEnv(),
        EngagementRecordsStore,
        { provide: MatDialog, useValue: dialog },
        { provide: NotificationService, useValue: notify },
      ],
    });
    store = TestBed.inject(EngagementRecordsStore);
    http = TestBed.inject(HttpTestingController);
    store.setAudit(audit());
    store.load();
    http.expectOne(`${BASE}/audits/a-1/engagement`).flush({ data: records() });
  });

  afterEach(() => http.verify());

  const closesWith = (value: unknown) => dialog.open.and.returnValue({ afterClosed: () => of(value) } as MatDialogRef<unknown>);

  it('declares independence with the audit version, then asks the page to reload the audit', () => {
    const reload = jasmine.createSpy('reload');
    store.auditChanged.subscribe(reload);
    closesWith({ hasConflict: true, conflictDetails: 'Former role in the area.' });

    store.declareIndependence();
    const req = http.expectOne(`${BASE}/audits/a-1/engagement/independence`);
    expect(req.request.body).toEqual({ hasConflict: true, conflictDetails: 'Former role in the area.', version: 'av1' });
    req.flush({ data: records({ auditVersion: 'av2' }) });

    expect(reload).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalled();

    // The next change carries the newest audit version.
    closesWith({ hasConflict: false, conflictDetails: null });
    store.declareIndependence();
    const next = http.expectOne(`${BASE}/audits/a-1/engagement/independence`);
    expect(next.request.body.version).toBe('av2');
    next.flush({ data: records({ auditVersion: 'av3' }) });
  });

  it('writes the first letter without a version and issues it with its own version, not the audit\'s', () => {
    const reload = jasmine.createSpy('reload');
    store.auditChanged.subscribe(reload);
    closesWith('Objectives and scope.');

    store.writeLetter('Starting text');
    const save = http.expectOne(`${BASE}/audits/a-1/engagement/letter`);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ body: 'Objectives and scope.', letterVersion: null });
    const drafted = records({
      letter: {
        id: 'l-1', status: 'draft', revision: 1, body: 'Objectives and scope.', draftedBy: 'u-lead', draftedAt: '', issuedBy: null,
        issuedAt: null, acknowledgedBy: null, acknowledgedAt: null, acknowledgementComment: null, previousRevisions: [], version: 'lv1',
      },
    });
    save.flush({ data: drafted });

    closesWith(true);
    store.issueLetter('Aud Itee');
    const issue = http.expectOne(`${BASE}/audits/a-1/engagement/letter/issue`);
    expect(issue.request.body).toEqual({ letterVersion: 'lv1' });
    issue.flush({ data: drafted });

    // The letter is its own record: the audit itself did not change.
    expect(reload).not.toHaveBeenCalled();
  });

  it('acknowledges by the letter, with an optional comment', () => {
    store.records.set(records({
      isAuditee: true, canAcknowledge: true, independence: null,
      letter: {
        id: 'l-1', status: 'issued', revision: 1, body: 'Letter.', draftedBy: 'u-lead', draftedAt: '', issuedBy: 'u-lead',
        issuedAt: '', acknowledgedBy: null, acknowledgedAt: null, acknowledgementComment: null, previousRevisions: [], version: 'lv2',
      },
    }));
    closesWith('');

    store.acknowledgeLetter();
    const req = http.expectOne(`${BASE}/engagement-letters/l-1/acknowledge`);
    expect(req.request.body).toEqual({ comment: null, letterVersion: 'lv2' });
    req.flush({ data: records() });
  });

  it('does nothing when a dialog is cancelled', () => {
    closesWith(undefined);
    store.declareIndependence();
    store.writeLetter('Starting text');
    http.expectNone(`${BASE}/audits/a-1/engagement/independence`);
    http.expectNone(`${BASE}/audits/a-1/engagement/letter`);
  });

  it('picks up the latest records when the page is out of date', () => {
    const reload = jasmine.createSpy('reload');
    store.auditChanged.subscribe(reload);
    closesWith({ hasConflict: false, conflictDetails: null });

    store.declareIndependence();
    http.expectOne(`${BASE}/audits/a-1/engagement/independence`).flush(
      { error_code: 'audit.concurrency_conflict', title: 'Conflict' },
      { status: 409, statusText: 'Conflict' },
    );

    expect(reload).toHaveBeenCalled();
    http.expectOne(`${BASE}/audits/a-1/engagement`).flush({ data: records({ auditVersion: 'av5' }) });
    expect(store.records()?.auditVersion).toBe('av5');
  });
});
