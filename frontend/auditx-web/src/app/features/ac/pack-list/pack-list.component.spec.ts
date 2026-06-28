import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { AcPackListComponent } from './pack-list.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { AcPackListItem, SessionDto } from '../../../core/models';

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

function listItem(overrides: Partial<AcPackListItem> = {}): AcPackListItem {
  return {
    id: 'p-1',
    versionNumber: 1,
    status: 'distributed',
    periodStart: '2026-01-01',
    periodEnd: '2026-03-31',
    acMeetingLabel: 'Q1 AC',
    sha256Hash: 'deadbeefcafef00d',
    producedFormats: ['html'],
    generatedBy: 'u-1',
    requestedAt: '2026-04-01T10:00:00Z',
    completedAt: '2026-04-01T10:01:00Z',
    ...overrides,
  };
}

describe('AcPackListComponent', () => {
  let fixture: ComponentFixture<AcPackListComponent>;
  let component: AcPackListComponent;
  let http: HttpTestingController;

  async function setup(
    perms: string[],
    items: AcPackListItem[],
  ): Promise<void> {
    TestBed.configureTestingModule({
      imports: [AcPackListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(AcPackListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    await fixture.whenStable();
    http
      .expectOne((r) => r.url === `${BASE}/ac-packs`)
      .flush({ data: { items, nextCursor: null, hasMore: false } });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('lists AC packs and renders status + version', async () => {
    await setup(['ViewACPacks'], [listItem()]);
    expect(component.packs().length).toBe(1);
    expect(component.state()).toBe('ready');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Distributed');
    expect(text).toContain('v1');
  });

  it('shows the empty state when no packs are returned', async () => {
    await setup(['ViewACPacks'], []);
    expect(component.isEmpty()).toBe(true);
  });

  it('hides the generate control without GenerateACPack', async () => {
    await setup(['ViewACPacks'], [listItem()]);
    expect(component.canGenerate()).toBe(false);
  });

  it('shows the generate control with GenerateACPack', async () => {
    await setup(['ViewACPacks', 'GenerateACPack'], [listItem()]);
    expect(component.canGenerate()).toBe(true);
  });

  it('re-queries with the status filter', async () => {
    await setup(['ViewACPacks'], [listItem()]);
    component.onStatusChange('approved');
    const req = http.expectOne((r) => r.url === `${BASE}/ac-packs`);
    expect(req.request.params.get('status')).toBe('approved');
    req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    await fixture.whenStable();
    expect(component.packs().length).toBe(0);
  });
});