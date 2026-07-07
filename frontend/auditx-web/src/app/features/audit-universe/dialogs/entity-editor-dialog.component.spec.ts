import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import {
  EntityEditorDialogComponent,
  EntityEditorDialogData,
} from './entity-editor-dialog.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Entity, RiskDimension, SessionDto } from '../../../core/models';

const BASE = '/api/v1';

function entity(overrides: Partial<Entity> = {}): Entity {
  return {
    id: 'e-1',
    entityType: 'Process',
    name: 'Wire Transfers',
    description: '',
    parentEntityId: null,
    ownerUserId: null,
    inherentScores: {},
    residualScores: {},
    compositeInherentScore: null,
    compositeResidualScore: null,
    lastAuditedAt: null,
    version: 2,
    ...overrides,
  };
}

function dimension(name: string, overrides: Partial<RiskDimension> = {}): RiskDimension {
  return {
    id: `d-${name}`,
    name,
    weight: 1,
    scaleMin: 1,
    scaleMax: 5,
    isActive: true,
    scaleLabelOverridesJson: null,
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

describe('EntityEditorDialogComponent', () => {
  let fixture: ComponentFixture<EntityEditorDialogComponent>;
  let component: EntityEditorDialogComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<EntityEditorDialogComponent>>;

  function setup(
    perms: string[] = ['ManageUniverse', 'ScoreRisk'],
    data?: EntityEditorDialogData,
  ): void {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialogRef = jasmine.createSpyObj<MatDialogRef<EntityEditorDialogComponent>>(
      'MatDialogRef',
      ['close'],
    );

    const dialogData: EntityEditorDialogData = data ?? {
      entity: entity(),
      entityTypes: ['Process', 'System'],
      parentCandidates: [],
    };

    TestBed.configureTestingModule({
      imports: [EntityEditorDialogComponent],
      providers: [
        provideTestEnv(),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: dialogData },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(EntityEditorDialogComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // The owner select lazily loads the user directory on construction.
    http.expectOne((r) => r.url === `${BASE}/users/directory`).flush({
      data: { items: [], nextCursor: null, hasMore: false },
    });
  }

  afterEach(() => http.verify());

  it('loads active dimensions and builds the scoring grid in edit mode', () => {
    setup();
    const req = http.expectOne((r) => r.url === `${BASE}/risk-dimensions`);
    expect(req.request.params.get('active')).toBe('true');
    req.flush({ data: [dimension('Financial'), dimension('Operational')] });

    expect(component.scoreRows().length).toBe(2);
    expect(component.scoreRows()[0].dimension.name).toBe('Financial');
  });

  it('seeds existing scores into the grid rows', () => {
    setup(['ManageUniverse', 'ScoreRisk'], {
      entity: entity({ inherentScores: { Financial: 3 } }),
      entityTypes: ['Process'],
      parentCandidates: [],
    });
    http
      .expectOne((r) => r.url === `${BASE}/risk-dimensions`)
      .flush({ data: [dimension('Financial')] });

    expect(component.scoreRows()[0].inherent).toBe(3);
  });

  it('clamps a typed score into the dimension scale', () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/risk-dimensions`)
      .flush({ data: [dimension('Financial', { scaleMin: 1, scaleMax: 5 })] });

    component.setScore(0, 'inherent', '9');
    expect(component.scoreRows()[0].inherent).toBe(5);
    component.setScore(0, 'inherent', '0');
    expect(component.scoreRows()[0].inherent).toBe(1);
    component.setScore(0, 'inherent', '');
    expect(component.scoreRows()[0].inherent).toBeNull();
  });

  it('rejects a partially scored side without calling the API', () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/risk-dimensions`)
      .flush({ data: [dimension('Financial'), dimension('Operational')] });

    component.setScore(0, 'inherent', '3'); // only one of two dimensions
    component.saveScores();

    expect(notify.warning).toHaveBeenCalled();
    http.expectNone(`${BASE}/audit-universe/entities/e-1/risk-scores`);
  });

  it('saves a fully scored side with the current version', () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/risk-dimensions`)
      .flush({ data: [dimension('Financial'), dimension('Operational')] });

    component.setScore(0, 'inherent', '3');
    component.setScore(1, 'inherent', '4');
    component.saveScores();

    const req = http.expectOne(
      `${BASE}/audit-universe/entities/e-1/risk-scores`,
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body.version).toBe(2);
    expect(req.request.body.inherentScores).toEqual({
      Financial: 3,
      Operational: 4,
    });
    expect(req.request.body.residualScores).toBeUndefined();

    req.flush({ data: entity({ compositeInherentScore: 3.5, version: 3 }) });

    // grid reloads its dimensions after a successful save
    http
      .expectOne((r) => r.url === `${BASE}/risk-dimensions`)
      .flush({ data: [dimension('Financial'), dimension('Operational')] });

    expect(notify.success).toHaveBeenCalled();
    expect(component.composite().inherent).toBe(3.5);
  });

  it('surfaces a 409 conflict when saving scores', () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/risk-dimensions`)
      .flush({ data: [dimension('Financial')] });

    component.setScore(0, 'residual', '2');
    component.saveScores();

    http
      .expectOne(`${BASE}/audit-universe/entities/e-1/risk-scores`)
      .flush(
        { title: 'Conflict' },
        { status: 409, statusText: 'Conflict' },
      );

    expect(notify.error).toHaveBeenCalledWith(
      jasmine.stringMatching(/changed by someone else/i),
    );
  });

  it('passes version on a metadata PATCH and surfaces 409', () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/risk-dimensions`)
      .flush({ data: [] });

    component.form.controls.name.setValue('Renamed');
    component.saveMeta();

    const req = http.expectOne(`${BASE}/audit-universe/entities/e-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.version).toBe(2);

    req.flush({ title: 'Conflict' }, { status: 409, statusText: 'Conflict' });
    expect(notify.error).toHaveBeenCalledWith(
      jasmine.stringMatching(/changed by someone else|cycle/i),
    );
  });
});
