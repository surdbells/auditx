import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { RecurrenceClusterDetailComponent } from './recurrence-cluster-detail.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { RecurrenceClusterDetail, SessionDto } from '../../../core/models';

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

function detail(): RecurrenceClusterDetail {
  return {
    id: 'c-1',
    auditableEntityId: 'e-1',
    category: 'aml',
    closedExceptionCount: 2,
    windowMonths: 12,
    firstOccurredAt: '2026-01-01T00:00:00Z',
    lastOccurredAt: '2026-05-01T00:00:00Z',
    detectedAt: '2026-06-01T00:00:00Z',
    notifiedAt: null,
    members: [
      {
        exceptionId: 'ex-1',
        auditId: 'au-1',
        title: 'Repeat KYC gap',
        severity: 'high',
        status: 'closed',
        raisedAt: '2026-02-01T00:00:00Z',
        closedAt: '2026-03-01T00:00:00Z',
      },
    ],
  };
}

describe('RecurrenceClusterDetailComponent', () => {
  let fixture: ComponentFixture<RecurrenceClusterDetailComponent>;
  let component: RecurrenceClusterDetailComponent;
  let http: HttpTestingController;

  async function setup(perms: string[]): Promise<void> {
    TestBed.configureTestingModule({
      imports: [RecurrenceClusterDetailComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(RecurrenceClusterDetailComponent);
    fixture.componentRef.setInput('id', 'c-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    await fixture.whenStable();
    http
      .expectOne(`${BASE}/analytics/recurrence-clusters/c-1`)
      .flush({ data: detail() });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads the cluster and its member exceptions', async () => {
    await setup(['ViewAnalytics', 'ViewExceptions']);
    expect(component.cluster()?.members.length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Repeat KYC gap');
  });

  it('links members to the exception detail only with ViewExceptions', async () => {
    await setup(['ViewAnalytics']);
    expect(component.canViewExceptions()).toBe(false);
    const link = (fixture.nativeElement as HTMLElement).querySelector(
      'a[href*="/exceptions/ex-1"]',
    );
    expect(link).toBeNull();
  });

  it('exposes the exception link when permitted', async () => {
    await setup(['ViewAnalytics', 'ViewExceptions']);
    expect(component.canViewExceptions()).toBe(true);
    const link = (fixture.nativeElement as HTMLElement).querySelector(
      'a[href*="/exceptions/ex-1"]',
    );
    expect(link).not.toBeNull();
  });
});
