import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { MakerCheckerGatesComponent } from './maker-checker-gates.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { AuthService } from '../../../../core/services/auth.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { MakerCheckerGateDto, RoleDto, SessionDto } from '../../../../core/models';

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

function role(name: string): RoleDto {
  return {
    id: name,
    name,
    description: '',
    isBuiltIn: true,
    isArchived: false,
    parentRoleIds: [],
    permissions: [],
  };
}

function gate(actionType: string, isEnabled: boolean): MakerCheckerGateDto {
  return { actionType, isEnabled, checkerRoleName: null, allowMakerAsChecker: false };
}

describe('MakerCheckerGatesComponent', () => {
  let fixture: ComponentFixture<MakerCheckerGatesComponent>;
  let component: MakerCheckerGatesComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;

  async function setup(perms: string[] = ['ManageInstitutionSettings']): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'error',
      'info',
      'warning',
    ]);

    TestBed.configureTestingModule({
      imports: [MakerCheckerGatesComponent],
      providers: [
        provideTestEnv(),
        { provide: NotificationService, useValue: notify },
      ],
    });
    TestBed.inject(AuthService).setSession(session(perms));

    fixture = TestBed.createComponent(MakerCheckerGatesComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();

    for (const req of http.match((r) => r.url === `${BASE}/roles`)) {
      req.flush({ data: [role('Audit Manager')] });
    }
    http.expectOne(`${BASE}/maker-checker/gates`).flush({
      data: [
        gate('role_permission_change', true),
        gate('template_publish', false),
      ],
    });
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads the configurable gates and roles', async () => {
    await setup();
    expect(component.state()).toBe('ready');
    expect(component.gates().length).toBe(2);
    expect(component.roleNames()).toEqual(['Audit Manager']);
  });

  it('persists a toggle and reflects the server result', async () => {
    await setup();
    const target = component.gates()[0]; // role_permission_change, enabled

    component.toggleEnabled(target, false);

    const req = http.expectOne(`${BASE}/maker-checker/gates`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      actionType: 'role_permission_change',
      isEnabled: false,
      checkerRoleName: null,
      allowMakerAsChecker: false,
    });
    req.flush({ data: gate('role_permission_change', false) });

    expect(
      component.gates().find((g) => g.actionType === 'role_permission_change')
        ?.isEnabled,
    ).toBe(false);
    expect(component.isSaving('role_permission_change')).toBe(false);
    expect(notify.success).toHaveBeenCalled();
  });
});
