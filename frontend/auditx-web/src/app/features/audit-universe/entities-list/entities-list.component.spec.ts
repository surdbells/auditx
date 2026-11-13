import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';

import { EntitiesListComponent } from './entities-list.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Entity, SessionDto } from '../../../core/models';

const BASE = '/api/v1';

function entity(overrides: Partial<Entity> = {}): Entity {
  return {
    id: 'e-1',
    entityType: 'branch',
    name: 'Ikeja Branch',
    description: null,
    parentEntityId: null,
    ownerUserId: null,
    orgUnitId: null,
    inherentScores: {},
    residualScores: {},
    compositeInherentScore: null,
    compositeResidualScore: null,
    lastAuditedAt: null,
    expectedAuditsPerYear: null,
    version: 1,
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

describe('EntitiesListComponent', () => {
  let fixture: ComponentFixture<EntitiesListComponent>;
  let component: EntitiesListComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  /** Flush the lookups the page warms on load (owner directory + entity-type reference data), if they fired. */
  function drainLookups(): void {
    for (const req of http.match((r) => r.url === `${BASE}/users/directory`)) {
      req.flush({ data: { items: [], total: 0, page: 1, pageSize: 5000, totalPages: 0, hasPrevious: false, hasNext: false } });
    }
    for (const req of http.match((r) => r.url === `${BASE}/reference-data/entity_type`)) {
      req.flush({ data: [] });
    }
  }

  function flushList(items: Entity[]): void {
    http
      .expectOne((r) => r.url === `${BASE}/audit-universe/entities`)
      .flush({ data: { items, total: items.length, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });
  }

  async function setup(permissions: string[], items: Entity[]): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'info', 'warning', 'error']);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    TestBed.configureTestingModule({
      imports: [EntitiesListComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    TestBed.inject(AuthService).setSession(session(permissions));

    fixture = TestBed.createComponent(EntitiesListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    flushList(items);
    drainLookups();
    fixture.detectChanges();
    await fixture.whenStable();
    drainLookups();
  }

  afterEach(() => {
    drainLookups();
    http.verify();
  });

  it('marks an archived entity so it can be told apart from the live universe', async () => {
    await setup(['ViewUniverse', 'ManageUniverse'], [
      entity(),
      entity({ id: 'e-2', name: 'Old Kiosk', isArchived: true, archivedAt: '2026-10-01T09:00:00Z' }),
    ]);

    const root = fixture.nativeElement as HTMLElement;
    const badges = root.querySelectorAll('.entities__archived-badge');
    expect(badges.length).toBe(1);
    expect(badges[0].textContent).toContain('Archived');
    expect(root.querySelectorAll('.entities__row--archived').length).toBe(1);
  });

  it('restores an archived entity and reloads the list', async () => {
    const archived = entity({ id: 'e-2', name: 'Old Kiosk', isArchived: true, archivedAt: '2026-10-01T09:00:00Z' });
    await setup(['ViewUniverse', 'ManageUniverse'], [archived]);

    component.restore(archived, new Event('click'));

    const req = http.expectOne(`${BASE}/audit-universe/entities/e-2/restore`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: { ...archived, isArchived: false, archivedAt: null } });

    // The page reloads so the row comes back as a live entity.
    flushList([{ ...archived, isArchived: false, archivedAt: null }]);
    fixture.detectChanges();

    expect(notify.success).toHaveBeenCalledWith(jasmine.stringContaining('Old Kiosk'));
    expect(component.entities()[0].isArchived).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('.entities__archived-badge')).toBeNull();
  });

  it('does not open the editor for an archived entity — it is read-only until restored', async () => {
    const archived = entity({ id: 'e-2', name: 'Old Kiosk', isArchived: true });
    await setup(['ViewUniverse', 'ManageUniverse'], [archived]);

    component.open(archived);

    expect(dialog.open).not.toHaveBeenCalled();
  });

  it('never offers an archived entity as a parent', async () => {
    const live = entity();
    const other = entity({ id: 'e-3', name: 'Lekki Branch' });
    const archived = entity({ id: 'e-2', name: 'Old Kiosk', isArchived: true });
    await setup(['ViewUniverse', 'ManageUniverse'], [live, other, archived]);
    dialog.open.and.returnValue({ afterClosed: () => ({ subscribe: () => undefined }) } as never);

    component.open(live);

    const data = dialog.open.calls.mostRecent().args[1]!.data as { parentCandidates: Entity[] };
    expect(data.parentCandidates.map((e) => e.id)).toEqual(['e-3']);
  });
});
