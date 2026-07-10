import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { AcPackViewerComponent } from './pack-viewer.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { AcPack, AcPackAnalytics, SessionDto } from '../../../core/models';

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

function pack(overrides: Partial<AcPack> = {}): AcPack {
  return {
    id: 'p-1',
    versionNumber: 1,
    status: 'pending_review',
    periodStart: '2026-01-01',
    periodEnd: '2026-03-31',
    acMeetingLabel: 'Q1 AC',
    sha256Hash: 'deadbeefcafef00d',
    ciaSupplementaryText: null,
    requestedFormats: ['html'],
    producedArtefacts: [
      { format: 'html', contentType: 'text/html', sizeBytes: 2048, sha256: 'd' },
    ],
    failureReason: null,
    generatedBy: 'u-1',
    requestedAt: '2026-04-01T10:00:00Z',
    completedAt: '2026-04-01T10:01:00Z',
    approvedBy: null,
    approvedAt: null,
    version: 'v1',
    ...overrides,
  };
}

function analytics(restricted = false): AcPackAnalytics {
  return {
    versionNumber: 1,
    periodStart: '2026-01-01',
    periodEnd: '2026-03-31',
    totalPlans: 1,
    planItemsTotal: 10,
    planItemsCompleted: 7,
    planCompletionPercent: 70,
    openExceptionTotal: 4,
    averageClosureDays: 12.5,
    exceptionsBySeverity: [{ severity: 'critical', count: 1 }],
    materialFindings: [
      {
        exceptionId: 'e-1',
        auditId: 'au-1',
        title: restricted ? null : 'AML control gap',
        severity: 'critical',
        status: 'open',
        auditableEntityId: null,
        raisedAt: '2026-02-01T10:00:00Z',
        targetDate: '2026-03-01',
        restricted,
      },
    ],
    sanctionsTotalCases: 0,
    sanctionsGridAdherencePercent: 0,
    sanctionsAppealRatePercent: 0,
    sanctionsByBusinessUnit: [],
    recurrenceClusters: [],
    generatedAtUtc: '2026-04-01T10:01:00Z',
  };
}

describe('AcPackViewerComponent', () => {
  let fixture: ComponentFixture<AcPackViewerComponent>;
  let component: AcPackViewerComponent;
  let http: HttpTestingController;

  async function setup(
    perms: string[],
    p: AcPack,
    a: AcPackAnalytics,
  ): Promise<void> {
    TestBed.configureTestingModule({
      imports: [AcPackViewerComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(AcPackViewerComponent);
    fixture.componentRef.setInput('id', 'p-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    await fixture.whenStable();
    http.expectOne(`${BASE}/ac-packs/p-1`).flush({ data: p });
    await fixture.whenStable();
    // analytics + distributions + comments fire after the pack resolves.
    http.expectOne(`${BASE}/ac-packs/p-1/analytics`).flush({ data: a });
    http.expectOne((r) => r.url === `${BASE}/ac-packs/p-1/distributions`).flush({
      data: {
        items: [],
        total: 0,
        page: 1,
        pageSize: 25,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      },
    });
    http
      .expectOne((r) => r.url === `${BASE}/ac-comments`)
      .flush({ data: [] });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads the pack and analytics snapshot', async () => {
    await setup(['ViewACPacks'], pack(), analytics());
    expect(component.state()).toBe('ready');
    expect(component.pack()?.versionNumber).toBe(1);
    expect(component.analytics()?.openExceptionTotal).toBe(4);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('AML control gap');
  });

  it('renders the restricted placeholder for restricted findings', async () => {
    await setup(['ViewACPacks'], pack(), analytics(true));
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Restricted — pending chair review');
    expect(text).not.toContain('AML control gap');
  });

  it('hides CIA controls without the CIA permission', async () => {
    await setup(['ViewACPacks'], pack(), analytics());
    expect(component.canCia()).toBe(false);
    expect(component.canApprove()).toBe(false);
    expect(component.canEditText()).toBe(false);
  });

  it('shows approve for a CIA user on a pending_review pack', async () => {
    await setup(['ViewACPacks', 'CIA'], pack({ status: 'pending_review' }), analytics());
    expect(component.canApprove()).toBe(true);
    expect(component.canDistribute()).toBe(false);
  });

  it('shows distribute for a CIA user on an approved pack', async () => {
    await setup(['ViewACPacks', 'CIA'], pack({ status: 'approved' }), analytics());
    expect(component.canDistribute()).toBe(true);
    expect(component.canApprove()).toBe(false);
  });
});