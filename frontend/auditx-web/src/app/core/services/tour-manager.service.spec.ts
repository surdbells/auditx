import { TestBed } from '@angular/core/testing';

import { TourManagerService } from './tour-manager.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { PageGuide } from '../models/page-guide.models';

function guide(overrides: Partial<PageGuide> = {}): PageGuide {
  return {
    id: 'test-page',
    titleKey: 'title',
    purposeKey: 'purpose',
    descriptionKey: 'description',
    ...overrides,
  };
}

describe('TourManagerService', () => {
  let service: TourManagerService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(TourManagerService);
  });

  afterEach(() => localStorage.clear());

  it('starts a tour with at least the welcome + completion steps', () => {
    service.start(guide());
    expect(service.activeGuideId()).toBe('test-page');
    // Welcome + completion are always present.
    expect(service.stepCount()).toBeGreaterThanOrEqual(2);
    expect(service.isFirst()).toBe(true);
  });

  it('includes a step per populated metadata group and skips empty ones', () => {
    const withActions = guide({ actionKeys: ['a', 'b'], tipKeys: ['t'] });
    service.start(withActions);
    const withActionsCount = service.stepCount();

    service.finish();
    localStorage.clear();

    service.start(guide()); // no actions/tips
    expect(service.stepCount()).toBeLessThan(withActionsCount);
  });

  it('adds a spotlight step for each UI section', () => {
    service.start(guide({ sections: [{ selector: '.a', titleKey: 't', bodyKey: 'b' }] }));
    const withSection = service.steps().find((s) => s.selector === '.a');
    expect(withSection).toBeTruthy();
  });

  it('navigates forward, backward and clamps at the ends', () => {
    service.start(guide({ actionKeys: ['a'], tipKeys: ['t'] }));
    const last = service.stepCount() - 1;

    service.previous();
    expect(service.index()).toBe(0); // clamped at start

    service.goTo(2);
    expect(service.index()).toBe(2);

    for (let i = 0; i < 20; i++) {
      service.next();
    }
    // next() past the last step finishes the tour (closes it).
    expect(service.activeGuideId()).toBeNull();
    expect(service.hasCompleted('test-page')).toBe(true);
    expect(last).toBeGreaterThan(0);
  });

  it('finish marks the tour completed and closes it', () => {
    service.start(guide());
    expect(service.hasCompleted('test-page')).toBe(false);
    service.finish();
    expect(service.activeGuideId()).toBeNull();
    expect(service.hasCompleted('test-page')).toBe(true);
  });

  it('skip also marks completed so a first-run tour does not nag', () => {
    service.start(guide());
    service.skip();
    expect(service.activeGuideId()).toBeNull();
    expect(service.hasCompleted('test-page')).toBe(true);
  });
});
