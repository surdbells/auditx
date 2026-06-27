import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';

import { EvidenceIntegrityComponent } from './evidence-integrity.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { FlaggedEvidence, SessionDto } from '../../../core/models';

const BASE = '/api/v1';

function flagged(overrides: Partial<FlaggedEvidence> = {}): FlaggedEvidence {
  return {
    id: 'e-1',
    auditId: 'au-1',
    originalFilename: 'evidence.pdf',
    mimeType: 'application/pdf',
    sizeBytes: 1024,
    sha256Hash: 'abc123def456',
    uploadedAt: '2026-06-01T10:00:00Z',
    uploadedBy: 'u-1',
    ...overrides,
  };
}

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

describe('EvidenceIntegrityComponent', () => {
  let fixture: ComponentFixture<EvidenceIntegrityComponent>;
  let component: EvidenceIntegrityComponent;
  let http: HttpTestingController;

  function setup(permissions: string[] = ['AdminOps']): void {
    TestBed.configureTestingModule({
      imports: [EvidenceIntegrityComponent],
      providers: [provideTestEnv()],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(permissions));

    fixture = TestBed.createComponent(EvidenceIntegrityComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads flagged evidence and renders it', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/evidence/flagged`)
      .flush({ data: [flagged()] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.flagged().length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('evidence.pdf');
    expect(text).toContain('au-1');
    expect(text).toContain('abc123def456');
  });

  it('shows the empty state when there is no flagged evidence', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/evidence/flagged`)
      .flush({ data: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isEmpty()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No flagged evidence');
  });

  it('renders the error state when the load fails', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/evidence/flagged`)
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.state()).toBe('error');
  });

  it('unflags evidence with a resolution and refreshes', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/evidence/flagged`)
      .flush({ data: [flagged()] });
    await fixture.whenStable();

    // Stub the dialog so it resolves with a resolution note.
    const dialog = TestBed.inject(MatDialog);
    spyOn(dialog, 'open').and.returnValue({
      afterClosed: () => of({ resolution: 'false positive' }),
    } as ReturnType<MatDialog['open']>);

    component.unflag(flagged());

    const req = http.expectOne(`${BASE}/audits/au-1/evidence/e-1/unflag`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.resolution).toBe('false positive');
    req.flush(null, { status: 204, statusText: 'No Content' });

    // The component re-fetches after a successful unflag.
    http
      .expectOne((r) => r.url === `${BASE}/evidence/flagged`)
      .flush({ data: [] });
  });

  it('does nothing when the unflag dialog is dismissed', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/evidence/flagged`)
      .flush({ data: [flagged()] });
    await fixture.whenStable();

    const dialog = TestBed.inject(MatDialog);
    spyOn(dialog, 'open').and.returnValue({
      afterClosed: () => of(undefined),
    } as ReturnType<MatDialog['open']>);

    component.unflag(flagged());
    // No unflag request is issued; http.verify() in afterEach asserts this.
  });
});
