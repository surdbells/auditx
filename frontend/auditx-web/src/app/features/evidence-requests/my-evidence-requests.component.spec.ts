import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';

import { MyEvidenceRequestsComponent } from './my-evidence-requests.component';
import { provideTestEnv } from '../../../testing/test-providers';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { EvidenceFile, EvidenceRequest, SessionDto } from '../../core/models';

const BASE = '/api/v1';
const ME = 'u-auditee';

function file(overrides: Partial<EvidenceFile> = {}): EvidenceFile {
  return {
    id: 'f-1',
    auditId: 'a-1',
    contextType: 'evidence_request',
    contextId: 'r-1',
    originalFilename: 'december.pdf',
    mimeType: 'application/pdf',
    sizeBytes: 2048,
    sha256Hash: 'a'.repeat(64),
    uploadedBy: ME,
    uploadedAt: '2027-01-12T09:00:00Z',
    isFlagged: false,
    isRecalled: false,
    ...overrides,
  };
}

function request(files: EvidenceFile[], overrides: Partial<EvidenceRequest> = {}): EvidenceRequest {
  return {
    id: 'r-1',
    auditId: 'a-1',
    checklistItemId: null,
    exceptionId: null,
    purpose: 'review_document',
    title: 'December reconciliation',
    documentType: 'reconciliation',
    requestedByUserId: 'u-auditor',
    requestedFromUserId: ME,
    requestedOn: '2027-01-10',
    dueDate: null,
    status: files.some((f) => !f.isRecalled) ? 'received' : 'requested',
    receivedByUserId: null,
    receivedAt: null,
    waiveReason: null,
    notes: null,
    isOverdue: false,
    files,
    version: 'v1',
    ...overrides,
  };
}

function session(): SessionDto {
  return {
    userId: ME,
    email: 'auditee@bank.test',
    firstName: 'Aud',
    lastName: 'Itee',
    displayName: 'Aud Itee',
    status: 'active',
    roles: [],
    permissions: [],
    expiresAt: '',
    absoluteExpiresAt: '',
  };
}

describe('MyEvidenceRequestsComponent', () => {
  let fixture: ComponentFixture<MyEvidenceRequestsComponent>;
  let component: MyEvidenceRequestsComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  function setup(rows: EvidenceRequest[]): void {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'info', 'warning', 'error']);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    TestBed.configureTestingModule({
      imports: [MyEvidenceRequestsComponent],
      providers: [
        provideTestEnv(),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    TestBed.inject(AuthService).setSession(session());

    fixture = TestBed.createComponent(MyEvidenceRequestsComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne((r) => r.url === `${BASE}/my/evidence-requests`).flush({ data: rows });
    fixture.detectChanges();
  }

  /** The reason dialog resolves with the given result (undefined = cancelled). */
  function dialogReturns(result: { reason: string } | undefined): void {
    dialog.open.and.returnValue({ afterClosed: () => of(result) } as never);
  }

  const root = () => fixture.nativeElement as HTMLElement;
  const recallButtons = () => [...root().querySelectorAll('.req__recall')];

  afterEach(() => http.verify());

  it('offers Recall on a document I uploaded', () => {
    setup([request([file()])]);

    expect(recallButtons().length).toBe(1);
    expect(component.canRecall(file())).toBe(true);
  });

  it('does not offer Recall on a document someone else uploaded, or one already recalled', () => {
    setup([
      request([
        file({ id: 'f-other', uploadedBy: 'u-manager' }),
        file({ id: 'f-gone', isRecalled: true, recalledAt: '2027-01-13T10:00:00Z', recalledBy: ME, recallReason: 'Wrong month' }),
      ]),
    ]);

    expect(recallButtons().length).toBe(0);
  });

  it('recalls with a reason, and the document stays listed as history', () => {
    const live = file();
    setup([request([live])]);
    dialogReturns({ reason: '  Uploaded the wrong month.  ' });

    component.recall(component.requests()[0], live);

    const req = http.expectOne(`${BASE}/evidence-requests/r-1/files/f-1/recall`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ reason: 'Uploaded the wrong month.' });

    const recalled = file({
      isRecalled: true,
      recalledAt: '2027-01-13T10:00:00Z',
      recalledBy: ME,
      recallReason: 'Uploaded the wrong month.',
    });
    req.flush({ data: request([recalled]) }); // no live document left → the request is outstanding again
    fixture.detectChanges();

    // History remains: the file is still there, shown as recalled with the reason — and can no longer be opened.
    const row = root().querySelector('.req__file--recalled');
    expect(row?.textContent).toContain('december.pdf');
    expect(row?.textContent).toContain('Recalled');
    expect(row?.textContent).toContain('Uploaded the wrong month.');
    expect(row?.querySelector('button')).toBeNull();
    expect(recallButtons().length).toBe(0);

    // The auditee is told the request is outstanding again, and is offered a first upload (not "another").
    expect(notify.success).toHaveBeenCalledWith(jasmine.stringContaining('outstanding again'));
    expect(component.hasLiveFile(component.requests()[0])).toBe(false);
    expect(root().querySelector('.req__actions')?.textContent).toContain('Upload document');
  });

  it('does nothing when the reason dialog is cancelled', () => {
    const live = file();
    setup([request([live])]);
    dialogReturns(undefined);

    component.recall(component.requests()[0], live);

    expect(component.requests()[0].files[0].isRecalled).toBe(false);
    // afterEach's http.verify() proves no recall request was sent.
  });

  it('requires a reason — a blank one sends nothing', () => {
    const live = file();
    setup([request([live])]);
    dialogReturns({ reason: '   ' });

    component.recall(component.requests()[0], live);

    expect(notify.success).not.toHaveBeenCalled();
  });

  it('keeps the request received when another live document remains', () => {
    const first = file();
    const second = file({ id: 'f-2', originalFilename: 'scan-2.pdf' });
    setup([request([first, second])]);
    dialogReturns({ reason: 'Superseded by the second scan.' });

    component.recall(component.requests()[0], first);
    http.expectOne(`${BASE}/evidence-requests/r-1/files/f-1/recall`).flush({
      data: request([{ ...first, isRecalled: true, recallReason: 'Superseded by the second scan.' }, second], { status: 'received' }),
    });
    fixture.detectChanges();

    expect(component.requests()[0].status).toBe('received');
    expect(notify.success).toHaveBeenCalledWith(jasmine.stringContaining('stays on the record'));
    expect(root().querySelector('.req__actions')?.textContent).toContain('Upload another');
  });
});
