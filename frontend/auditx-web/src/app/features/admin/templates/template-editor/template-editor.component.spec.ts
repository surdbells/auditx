import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';

import { TemplateEditorComponent } from './template-editor.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { AuthService } from '../../../../core/services/auth.service';
import { NotificationService } from '../../../../core/services/notification.service';
import {
  SaveTemplateItemRequest,
  SessionDto,
  Template,
} from '../../../../core/models';

const BASE = '/api/v1';

function template(overrides: Partial<Template> = {}): Template {
  return {
    id: 't-1',
    name: 'AML Review',
    auditType: 'AML',
    description: 'desc',
    status: 'draft',
    currentVersion: 0,
    clonedFromTemplateId: null,
    items: [
      {
        id: 'i-1',
        prompt: 'Verify customer identity',
        referenceNotes: '',
        responseType: 'pass_fail_na',
        sectionName: '',
        orderIndex: 0,
        isRequired: true,
        defaultAssignmentRuleJson: null,
        responseConfigJson: null,
        riskRating: null,
        controlId: null,
      },
    ],
    sections: [],
    versions: [],
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

describe('TemplateEditorComponent', () => {
  let fixture: ComponentFixture<TemplateEditorComponent>;
  let component: TemplateEditorComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  async function setup(
    perms: string[] = ['ViewTemplates', 'ManageTemplates'],
    initial: Template = template(),
  ): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    TestBed.configureTestingModule({
      imports: [TemplateEditorComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(TemplateEditorComponent);
    fixture.componentRef.setInput('id', 't-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // bootstrap runs in a microtask; wait for it before expecting the GET.
    await fixture.whenStable();
    http.expectOne(`${BASE}/templates/t-1`).flush({ data: initial });
    // child versions component GET (also kicked off in a microtask)
    await fixture.whenStable();
    http.expectOne(`${BASE}/templates/t-1/versions`).flush({ data: [] });
    fixture.detectChanges();
  }

  /**
   * The audit-type select reads the reference-data lookup, which lazily GETs
   * the active `audit_type` items. Drain it so it doesn't leak (never-throw:
   * flushing an empty list is enough).
   */
  function flushAuditTypeLookup(): void {
    for (const req of http.match(
      (r) => r.url === `${BASE}/reference-data/audit_type`,
    )) {
      req.flush({ data: [] });
    }
  }

  afterEach(() => {
    flushAuditTypeLookup();
    http.verify();
  });

  it('loads and renders the template and its items', async () => {
    await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.state()).toBe('ready');
    expect(component.isDraft()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Verify customer identity');
  });

  it('adds an item via the dialog and refreshes the template', async () => {
    await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    const body: SaveTemplateItemRequest = {
      prompt: 'New check',
      referenceNotes: '',
      responseType: 'pass_fail_na',
      sectionName: '',
      isRequired: false,
      defaultAssignmentRuleJson: null,
    };
    dialog.open.and.returnValue({
      afterClosed: () => of(body),
    } as MatDialogRef<unknown>);

    component.addItem();

    const req = http.expectOne(`${BASE}/templates/t-1/items`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.prompt).toBe('New check');

    const updated = template({
      items: [
        ...template().items,
        {
          id: 'i-2',
          prompt: 'New check',
          referenceNotes: '',
          responseType: 'pass_fail_na',
          sectionName: '',
          orderIndex: 1,
          isRequired: false,
          defaultAssignmentRuleJson: null,
          responseConfigJson: null,
          riskRating: null,
          controlId: null,
        },
      ],
    });
    req.flush({ data: updated });

    expect(notify.success).toHaveBeenCalled();
    expect(component.template()?.items.length).toBe(2);
  });

  it('shows an info toast on a 202 pending publish', async () => {
    await setup();
    await fixture.whenStable();
    fixture.detectChanges();

    component.publish();

    http
      .expectOne(`${BASE}/templates/t-1/publish`)
      .flush(
        { data: { pendingActionId: 'pa-1' } },
        { status: 202, statusText: 'Accepted' },
      );

    expect(notify.info).toHaveBeenCalledWith(jasmine.stringMatching(/approval/i));
    // Status unchanged because the change is pending.
    expect(component.template()?.status).toBe('draft');
  });

  it('enters create-mode without loading when the route supplies no id (regression: /admin/templates/new)', async () => {
    // withComponentInputBinding() pushes `undefined` into the id input on the paramless
    // `/admin/templates/new` route (it does not preserve the 'new' default). The editor must
    // treat that as create-mode and NOT attempt GET /templates/undefined (which 404s → error page).
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    TestBed.configureTestingModule({
      imports: [TemplateEditorComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    TestBed.inject(AuthService).setSession(session(['ViewTemplates', 'ManageTemplates']));

    fixture = TestBed.createComponent(TemplateEditorComponent);
    fixture.componentRef.setInput('id', undefined);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isNew()).toBe(true);
    expect(component.state()).toBe('ready');
    // No load-by-id and no versions request in create-mode.
    http.expectNone((r) => r.url.includes('/templates'));
  });

  it('treats published templates as read-only', async () => {
    await setup(['ViewTemplates', 'ManageTemplates'], template({ status: 'published', currentVersion: 1 }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isPublished()).toBe(true);
    expect(component.canEditStructure()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('read-only');
  });
});
