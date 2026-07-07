import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AcCommentsComponent } from './ac-comments.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { AuthService } from '../../../../core/services/auth.service';
import { AcComment, SessionDto } from '../../../../core/models';

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

function comment(overrides: Partial<AcComment> = {}): AcComment {
  return {
    id: 'c-1',
    targetType: 'pack',
    targetId: 't-1',
    commentText: 'Looks complete.',
    authorUserId: 'u-2',
    commentedAt: '2026-04-02T10:00:00Z',
    ...overrides,
  };
}

describe('AcCommentsComponent', () => {
  let fixture: ComponentFixture<AcCommentsComponent>;
  let component: AcCommentsComponent;
  let http: HttpTestingController;

  async function setup(perms: string[], comments: AcComment[]): Promise<void> {
    TestBed.configureTestingModule({
      imports: [AcCommentsComponent],
      providers: [provideTestEnv()],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(AcCommentsComponent);
    fixture.componentRef.setInput('targetType', 'pack');
    fixture.componentRef.setInput('targetId', 't-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    await fixture.whenStable();
    http
      .expectOne((r) => r.url === `${BASE}/ac-comments`)
      .flush({ data: comments });
    await fixture.whenStable();
    fixture.detectChanges();

    // Rendering an author name lazily loads the user directory.
    flushUserDirectory();
  }

  /** Flushes the (at most one) lazy user-directory GET the author label triggers. */
  function flushUserDirectory(): void {
    for (const req of http.match((r) => r.url === `${BASE}/users/directory`)) {
      req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    }
  }

  afterEach(() => {
    flushUserDirectory();
    http.verify();
  });

  it('loads comments for the bound target', async () => {
    await setup(['ACMember'], [comment()]);
    expect(component.comments().length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Looks complete.');
  });

  it('posts a new comment and appends it', async () => {
    await setup(['ACMember'], []);
    component.draft = 'New observation.';
    component.post();
    const req = http.expectOne(`${BASE}/ac-comments`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.targetType).toBe('pack');
    expect(req.request.body.comment).toBe('New observation.');
    req.flush(
      { data: comment({ id: 'c-2', commentText: 'New observation.' }) },
      { status: 201, statusText: 'Created' },
    );
    expect(component.comments().length).toBe(1);
    expect(component.draft).toBe('');
  });

  it('hides the post form without ACMember', async () => {
    await setup([], [comment()]);
    expect(component.canComment()).toBe(false);
  });
});