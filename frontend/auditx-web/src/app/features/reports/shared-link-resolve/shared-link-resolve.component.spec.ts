import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';

import { SharedLinkResolveComponent } from './shared-link-resolve.component';
import { provideTestEnv } from '../../../../testing/test-providers';

const BASE = '/api/v1';

describe('SharedLinkResolveComponent', () => {
  let fixture: ComponentFixture<SharedLinkResolveComponent>;
  let component: SharedLinkResolveComponent;
  let http: HttpTestingController;
  let router: Router;

  async function setup(slug: string): Promise<void> {
    TestBed.configureTestingModule({
      imports: [SharedLinkResolveComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(SharedLinkResolveComponent);
    fixture.componentRef.setInput('slug', slug);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  it('forwards to the report route on a successful resolve', async () => {
    await setup('good-slug');
    http.expectOne(`${BASE}/shared-links/good-slug`).flush({
      data: { targetType: 'report', targetId: 'r-9' },
    });
    expect(router.navigate).toHaveBeenCalledWith(['/reports', 'r-9'], { replaceUrl: true });
  });

  it('shows the forbidden state on a 403 (a link is not an access grant)', async () => {
    await setup('secret');
    http.expectOne(`${BASE}/shared-links/secret`).flush(
      { title: 'Forbidden' },
      { status: 403, statusText: 'Forbidden' },
    );
    expect(component.state()).toBe('forbidden');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('shows the invalid state on a 404 (missing / revoked / expired)', async () => {
    await setup('gone');
    http.expectOne(`${BASE}/shared-links/gone`).flush(
      { title: 'Not found' },
      { status: 404, statusText: 'Not Found' },
    );
    expect(component.state()).toBe('invalid');
  });
});
