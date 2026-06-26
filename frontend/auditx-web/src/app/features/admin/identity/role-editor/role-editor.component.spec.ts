import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';

import { RoleEditorComponent } from './role-editor.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { NotificationService } from '../../../../core/services/notification.service';
import { PermissionDto } from '../../../../core/models';

const BASE = '/api/v1';

const catalogue: PermissionDto[] = [
  {
    key: 'users.read',
    label: 'View users',
    description: 'Read user records',
    module: 'Identity',
    finestScope: 'branch',
  },
  {
    key: 'audit.read',
    label: 'View audits',
    description: 'Read audit engagements',
    module: 'Audit',
    finestScope: 'global',
  },
];

describe('RoleEditorComponent (create)', () => {
  let fixture: ComponentFixture<RoleEditorComponent>;
  let component: RoleEditorComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let router: Router;

  function bootstrap(): void {
    http.expectOne((r) => r.url === `${BASE}/permissions/catalogue`).flush({
      data: catalogue,
    });
    http.expectOne((r) => r.url === `${BASE}/roles`).flush({ data: [] });
  }

  beforeEach(async () => {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);

    await TestBed.configureTestingModule({
      imports: [RoleEditorComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RoleEditorComponent);
    // id defaults to 'new'
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('builds permission groups by module from the catalogue', async () => {
    bootstrap();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.state()).toBe('ready');
    const modules = component.groups().map((g) => g.module);
    expect(modules).toContain('Identity');
    expect(modules).toContain('Audit');
  });

  it('warns and does not call the API when saving with no permissions selected', async () => {
    bootstrap();
    await fixture.whenStable();
    fixture.detectChanges();

    component.form.controls.name.setValue('New Role');
    component.save();

    expect(notify.warning).toHaveBeenCalled();
    http.expectNone((r) => r.method === 'POST' && r.url === `${BASE}/roles`);
  });

  it('shows a maker-checker info toast on a 202 pending response', async () => {
    bootstrap();
    await fixture.whenStable();
    fixture.detectChanges();

    const navSpy = spyOn(router, 'navigate').and.resolveTo(true);

    component.form.controls.name.setValue('Audit Viewer');
    const auditGroup = component.groups().find((g) => g.module === 'Audit')!;
    component.togglePermission(auditGroup.permissions[0], true);
    component.save();

    const req = http.expectOne((r) => r.method === 'POST' && r.url === `${BASE}/roles`);
    req.flush(
      { data: { pendingActionId: 'pa-1' } },
      { status: 202, statusText: 'Accepted' },
    );

    expect(notify.info).toHaveBeenCalledWith(jasmine.stringMatching(/maker-checker/i));
    expect(navSpy).toHaveBeenCalledWith(['/admin/roles']);
  });

  it('shows a success toast on a 201 created response', async () => {
    bootstrap();
    await fixture.whenStable();
    fixture.detectChanges();

    spyOn(router, 'navigate').and.resolveTo(true);

    component.form.controls.name.setValue('Audit Viewer');
    const auditGroup = component.groups().find((g) => g.module === 'Audit')!;
    component.togglePermission(auditGroup.permissions[0], true);
    component.save();

    const req = http.expectOne((r) => r.method === 'POST' && r.url === `${BASE}/roles`);
    req.flush(
      {
        data: {
          id: 'r-1',
          name: 'Audit Viewer',
          description: '',
          isBuiltIn: false,
          isArchived: false,
          parentRoleIds: [],
          permissions: [],
        },
      },
      { status: 201, statusText: 'Created' },
    );

    expect(notify.success).toHaveBeenCalled();
  });
});
