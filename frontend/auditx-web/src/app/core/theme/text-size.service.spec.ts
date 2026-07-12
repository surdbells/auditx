import { TestBed } from '@angular/core/testing';

import { TextSizeService } from './text-size.service';

describe('TextSizeService', () => {
  beforeEach(() => {
    localStorage.removeItem('auditx.textSize');
    document.documentElement.style.removeProperty('--ax-root-font');
  });

  afterEach(() => {
    localStorage.removeItem('auditx.textSize');
    document.documentElement.style.removeProperty('--ax-root-font');
  });

  it('defaults to medium and applies the root font-size var', () => {
    const svc = TestBed.runInInjectionContext(() => new TextSizeService());
    expect(svc.size()).toBe('md');
    expect(document.documentElement.style.getPropertyValue('--ax-root-font')).toBe('100%');
  });

  it('persists and applies a chosen size', () => {
    const svc = TestBed.runInInjectionContext(() => new TextSizeService());
    svc.use('lg');
    expect(svc.size()).toBe('lg');
    expect(localStorage.getItem('auditx.textSize')).toBe('lg');
    expect(document.documentElement.style.getPropertyValue('--ax-root-font')).toBe('112.5%');
  });

  it('restores the persisted size on construction', () => {
    localStorage.setItem('auditx.textSize', 'xl');
    const svc = TestBed.runInInjectionContext(() => new TextSizeService());
    expect(svc.size()).toBe('xl');
    expect(document.documentElement.style.getPropertyValue('--ax-root-font')).toBe('125%');
  });
});
