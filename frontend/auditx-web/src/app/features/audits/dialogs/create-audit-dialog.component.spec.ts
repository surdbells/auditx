import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { provideTestEnv } from '../../../../testing/test-providers';
import { CreateAuditRequest, UserDto } from '../../../core/models';
import { CreateAuditDialogComponent } from './create-audit-dialog.component';

function user(id: string, displayName: string): UserDto {
  return { id, displayName, email: `${id}@bank.test` } as UserDto;
}

// Five auditors and a lead — the arrangement the two-sides rule exists for.
const USERS = [
  user('lead', 'Lena Lead'),
  user('a1', 'Auditor One'),
  user('a2', 'Auditor Two'),
  user('a3', 'Auditor Three'),
  user('a4', 'Auditor Four'),
  user('a5', 'Auditor Five'),
];

describe('CreateAuditDialogComponent — auditees and the audit team', () => {
  let http: HttpTestingController;
  let dialogRef: jasmine.SpyObj<MatDialogRef<CreateAuditDialogComponent, CreateAuditRequest>>;

  function create() {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    TestBed.configureTestingModule({
      imports: [CreateAuditDialogComponent],
      providers: [
        ...provideTestEnv(),
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: { users: USERS, templates: [] } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(CreateAuditDialogComponent);
    fixture.detectChanges();
    // Audit types are looked up lazily from reference data.
    http.match((r) => r.url.includes('/reference-data')).forEach((r) => r.flush({ data: [] }));
    return fixture.componentInstance;
  }

  const ids = (users: UserDto[]) => users.map((u) => u.id);

  it('never offers an auditee as a team member, nor a team member as an auditee', () => {
    const component = create();
    component.form.patchValue({ leadUserId: 'lead', auditeeUserId: 'a3' });

    // The lead and the primary auditee are on neither list.
    expect(ids(component.teamCandidates())).toEqual(['a1', 'a2', 'a4', 'a5']);
    expect(ids(component.otherAuditeeCandidates())).toEqual(['a1', 'a2', 'a4', 'a5']);

    // Two more auditees: they disappear from the team list.
    component.form.patchValue({ additionalAuditeeUserIds: ['a4', 'a5'] });
    expect(ids(component.teamCandidates())).toEqual(['a1', 'a2']);

    // Two auditors on the team: they disappear from the auditee list.
    component.form.patchValue({ teamMemberUserIds: ['a1', 'a2'] });
    expect(ids(component.otherAuditeeCandidates())).toEqual(['a4', 'a5']);
  });

  it('sends two auditors and three auditees as separate lists', () => {
    const component = create();
    component.form.patchValue({
      name: 'Audit A',
      auditType: 'branch',
      startDate: new Date(2027, 0, 10),
      leadUserId: 'lead',
      auditeeUserId: 'a3',
      additionalAuditeeUserIds: ['a4', 'a5'],
      teamMemberUserIds: ['a1', 'a2'],
    });
    component.submit();

    const request = dialogRef.close.calls.mostRecent().args[0]!;
    expect(request.auditeeUserId).toBe('a3');
    expect(request.additionalAuditeeUserIds).toEqual(['a4', 'a5']);
    expect(request.teamMemberUserIds).toEqual(['a1', 'a2']);
  });

  it('keeps someone who became an auditee off the team even if they were picked as a team member first', () => {
    const component = create();
    component.form.patchValue({
      name: 'Audit A',
      auditType: 'branch',
      startDate: new Date(2027, 0, 10),
      leadUserId: 'lead',
      auditeeUserId: 'a3',
      teamMemberUserIds: ['a1', 'a2', 'a4'],
    });
    // a4 was chosen for the team, then named as an auditee as well; a2 then became the primary auditee.
    component.form.patchValue({ additionalAuditeeUserIds: ['a4'], auditeeUserId: 'a2' });
    component.submit();

    const request = dialogRef.close.calls.mostRecent().args[0]!;
    expect(request.auditeeUserId).toBe('a2');
    expect(request.additionalAuditeeUserIds).toEqual(['a4']);
    expect(request.teamMemberUserIds).toEqual(['a1']);
  });
});
