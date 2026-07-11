import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { BrandingService } from './branding.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { Branding } from '../models';

const BASE = '/api/v1';

function branding(overrides: Partial<Branding> = {}): Branding {
  return {
    organizationName: 'ACME Bank',
    primaryColor: '#112233',
    accentColor: '#445566',
    logoDataUri: null,
    iconDataUri: null,
    showOverview: true,
    showWalkthrough: true,
    autoStartWalkthrough: true,
    ...overrides,
  };
}

describe('BrandingService', () => {
  let service: BrandingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(BrandingService);
    http = TestBed.inject(HttpTestingController);
    // Reset any custom properties a previous test may have set.
    const root = document.documentElement;
    for (const prop of [
      '--mat-sys-primary',
      '--mat-sys-tertiary',
      '--color-brand-600',
      '--color-accent-600',
    ]) {
      root.style.removeProperty(prop);
    }
  });

  afterEach(() => http.verify());

  it('fetches /branding and applies the theme colours + name', () => {
    service.load().subscribe();
    const req = http.expectOne(`${BASE}/branding`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: branding() });

    const root = document.documentElement;
    expect(service.organizationName()).toBe('ACME Bank');
    expect(root.style.getPropertyValue('--mat-sys-primary')).toBe('#112233');
    expect(root.style.getPropertyValue('--color-brand-600')).toBe('#112233');
    expect(root.style.getPropertyValue('--mat-sys-tertiary')).toBe('#445566');
    expect(root.style.getPropertyValue('--color-accent-600')).toBe('#445566');
  });

  it('exposes a logo and swaps the favicon when supplied', () => {
    service.apply(
      branding({
        logoDataUri: 'data:image/png;base64,LOGO',
        iconDataUri: 'data:image/png;base64,ICON',
      }),
    );
    expect(service.logoDataUri()).toBe('data:image/png;base64,LOGO');
    const link = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    expect(link?.href).toContain('data:image/png;base64,ICON');
  });

  it('falls back to the default name when branding is blank', () => {
    service.apply(branding({ organizationName: '   ' }));
    expect(service.organizationName()).toBe('AuditX');
  });

  it('picks a dark foreground on a light primary colour', () => {
    service.apply(branding({ primaryColor: '#ffffff' }));
    expect(
      document.documentElement.style.getPropertyValue('--mat-sys-on-primary'),
    ).toBe('#0f172a');
  });
});
