import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';

import { ReportViewerComponent } from './report-viewer.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Report, SessionDto } from '../../../core/models';

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

function report(overrides: Partial<Report> = {}): Report {
  return {
    id: 'r-1',
    auditId: 'au-1',
    kind: 'audit_engagement',
    versionNumber: 3,
    status: 'completed',
    sha256Hash: 'abc',
    templateId: 't-1',
    templateVersionSnapshot: 1,
    requestedFormats: ['html'],
    producedArtefacts: [],
    failureReason: null,
    generatedBy: 'u-preparer',
    requestedAt: '2026-06-01T10:00:00Z',
    completedAt: '2026-06-01T10:01:00Z',
    version: 'v1',
    stage: 'final',
    signOffStatus: 'awaiting',
    isDistributed: false,
    isDistributable: false,
    ...overrides,
  };
}

describe('ReportViewerComponent — draft and final review', () => {
  let fixture: ComponentFixture<ReportViewerComponent>;
  let component: ReportViewerComponent;
  let http: HttpTestingController;
  let dialog: jasmine.SpyObj<MatDialog>;

  async function setup(perms: string[]): Promise<void> {
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    TestBed.configureTestingModule({
      imports: [ReportViewerComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: MatDialog, useValue: dialog },
        {
          provide: NotificationService,
          useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'info', 'warning', 'error']),
        },
      ],
    });
    TestBed.inject(AuthService).setSession(session(perms));
    fixture = TestBed.createComponent(ReportViewerComponent);
    fixture.componentRef.setInput('id', 'r-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  }

  /** The page's own loads (report, preview, audit, distributions, comments) are not what these specs are about. */
  function flushLoads(): void {
    http.match(() => true).forEach((req) => {
      if (!req.cancelled) {
        req.flush({ data: [] });
      }
    });
  }

  afterEach(() => flushLoads());

  it('lets the Chief Internal Auditor decide a final awaiting sign-off, unless they prepared it', async () => {
    await setup(['ViewReport', 'CIA']);
    component.report.set(report());
    expect(component.canDecide()).toBe(true);
    expect(component.preparedByMe()).toBe(false);

    component.report.set(report({ generatedBy: 'u1' }));
    expect(component.canDecide()).toBe(false);
    expect(component.preparedByMe()).toBe(true);

    component.report.set(report({ signOffStatus: 'signed_off', isDistributable: true }));
    expect(component.canDecide()).toBe(false);
  });

  it('offers no sign-off without the Chief Internal Auditor authority', async () => {
    await setup(['ViewReport', 'DistributeReport']);
    component.report.set(report());
    expect(component.canDecide()).toBe(false);
  });

  it('distributes a draft, or a final only once signed off', async () => {
    await setup(['ViewReport', 'DistributeReport']);
    component.report.set(report({ stage: 'draft', signOffStatus: 'not_required', isDistributable: true }));
    expect(component.canDistributeNow()).toBe(true);

    component.report.set(report());
    expect(component.canDistributeNow()).toBe(false);

    component.report.set(report({ signOffStatus: 'returned' }));
    expect(component.canDistributeNow()).toBe(false);

    component.report.set(report({ signOffStatus: 'signed_off', isDistributable: true }));
    expect(component.canDistributeNow()).toBe(true);
  });

  it('shows the comment thread on a completed draft only', async () => {
    await setup(['ViewReport']);
    component.report.set(report({ stage: 'draft', signOffStatus: 'not_required' }));
    expect(component.showComments()).toBe(true);

    component.report.set(report({ stage: 'draft', signOffStatus: 'not_required', status: 'running' }));
    expect(component.showComments()).toBe(false);

    component.report.set(report());
    expect(component.showComments()).toBe(false);

    component.report.set(report({ auditId: null, kind: 'executive_summary', stage: 'final', signOffStatus: 'not_required' }));
    expect(component.isEngagement()).toBe(false);
    expect(component.showComments()).toBe(false);
  });

  it('signs off through the dialog and shows the updated report', async () => {
    await setup(['ViewReport', 'CIA']);
    flushLoads();
    component.report.set(report());
    dialog.open.and.returnValue({ afterClosed: () => of('Approved for issue.') } as never);

    component.decide('sign_off');
    const req = http.expectOne(`${BASE}/reports/r-1/sign-off`);
    expect(req.request.body).toEqual({ note: 'Approved for issue.' });
    req.flush({ data: report({ signOffStatus: 'signed_off', signOffBy: 'u1', isDistributable: true }) });

    expect(component.report()?.signOffStatus).toBe('signed_off');
    expect(component.deciding()).toBe(false);
  });

  it('does nothing when the decision dialog is cancelled', async () => {
    await setup(['ViewReport', 'CIA']);
    flushLoads();
    component.report.set(report());
    dialog.open.and.returnValue({ afterClosed: () => of(undefined) } as never);

    component.decide('return');
    http.expectNone(`${BASE}/reports/r-1/return`);
    expect(component.report()?.signOffStatus).toBe('awaiting');
  });
});
