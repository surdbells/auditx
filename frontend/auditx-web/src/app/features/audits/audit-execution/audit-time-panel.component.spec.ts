import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AuditTimePanelComponent } from './audit-time-panel.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { Audit, SessionDto, TimeSummary } from '../../../core/models';

const BASE = '/api/v1';

function session(permissions: string[]): SessionDto {
  return {
    userId: 'u1',
    email: 'a@b.c',
    firstName: 'A',
    lastName: 'B',
    displayName: 'A B',
    status: 'active',
    roles: [],
    permissions,
    expiresAt: '',
    absoluteExpiresAt: '',
  };
}

function audit(): Audit {
  return {
    id: 'a-1',
    name: 'Audit',
    scopeDescription: null,
    auditType: 'branch_operations',
    status: 'in_progress',
    startDate: '2026-01-01',
    targetEndDate: null,
    actualEndDate: null,
    templateId: null,
    templateVersion: null,
    planItemId: null,
    leadUserId: 'u1',
    auditeeUserId: 'u2',
    cancellationReason: null,
    budgetedHours: 40,
    isSelfAssessment: false,
    kickoffScheduledAtUtc: null,
    kickoffLocation: null,
    kickoffAgenda: null,
    version: 'v1',
    teamMembers: [],
    sections: [],
    checklistItems: [
      {
        id: 'i-1', sectionName: 'Cash', orderIndex: 0, prompt: 'Count cash',
        referenceNotes: null, responseType: 'pass_fail_na', assignedUserId: null,
        isRequired: true, itemState: 'not_started',
      },
    ],
  };
}

function emptySummary(): TimeSummary {
  return {
    auditId: 'a-1', budgetedHours: 40, actualHours: 0, varianceHours: 40,
    percentConsumed: 0, entryCount: 0, contributorCount: 0, byCategory: [], byUser: [],
  };
}

describe('AuditTimePanelComponent', () => {
  let fixture: ComponentFixture<AuditTimePanelComponent>;
  let component: AuditTimePanelComponent;
  let http: HttpTestingController;

  function setup(scopedItemId: string | null = 'i-1'): void {
    TestBed.configureTestingModule({
      imports: [AuditTimePanelComponent],
      providers: [provideTestEnv()],
    });
    TestBed.inject(AuthService).setSession(session(['LogTime']));

    fixture = TestBed.createComponent(AuditTimePanelComponent);
    fixture.componentRef.setInput('audit', audit());
    fixture.componentRef.setInput('scopedItemId', scopedItemId);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // The panel loads its entries + summary on init.
    http.expectOne(`${BASE}/audits/a-1/time-entries`).flush({ data: [] });
    http.expectOne(`${BASE}/audits/a-1/time-entries/summary`).flush({ data: emptySummary() });
  }

  afterEach(() => http.verify());

  it('quick-logs a preset against the scoped item, today, as fieldwork', () => {
    setup('i-1');
    component.logQuick(0.5);

    const req = http.expectOne(`${BASE}/audits/a-1/time-entries`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.hours).toBe(0.5);
    expect(req.request.body.category).toBe('fieldwork');
    expect(req.request.body.checklistItemId).toBe('i-1'); // attached to the open item
    expect(req.request.body.workDate).toMatch(/^\d{4}-\d{2}-\d{2}$/); // today
    req.flush({ data: {} });

    // A successful log refreshes the list + summary.
    http.expectOne(`${BASE}/audits/a-1/time-entries`).flush({ data: [] });
    http.expectOne(`${BASE}/audits/a-1/time-entries/summary`).flush({ data: emptySummary() });
  });

  it('quick-logs with no item when unscoped (whole-audit view)', () => {
    setup(null);
    component.logQuick(1);
    const req = http.expectOne(`${BASE}/audits/a-1/time-entries`);
    expect(req.request.body.checklistItemId).toBeNull();
    req.flush({ data: {} });
    http.expectOne(`${BASE}/audits/a-1/time-entries`).flush({ data: [] });
    http.expectOne(`${BASE}/audits/a-1/time-entries/summary`).flush({ data: emptySummary() });
  });

  it('stops the timer and auto-logs the measured time (rounded up to 15 min)', () => {
    setup('i-1');
    component.elapsedSec.set(40 * 60); // 40 min → rounds up to 0.75h
    component.stopTimer();

    expect(component.timerRunning()).toBe(false);
    expect(component.elapsedSec()).toBe(0);
    const req = http.expectOne(`${BASE}/audits/a-1/time-entries`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.hours).toBe(0.75);
    expect(req.request.body.category).toBe('fieldwork');
    expect(req.request.body.checklistItemId).toBe('i-1');
    req.flush({ data: {} });
    // Refreshes the list + summary after logging.
    http.expectOne(`${BASE}/audits/a-1/time-entries`).flush({ data: [] });
    http.expectOne(`${BASE}/audits/a-1/time-entries/summary`).flush({ data: emptySummary() });
  });
});
