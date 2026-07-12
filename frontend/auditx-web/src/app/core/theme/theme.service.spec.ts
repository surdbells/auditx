import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { provideTestEnv } from '../../../testing/test-providers';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  function make(): ThemeService {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    const svc = TestBed.inject(ThemeService);
    TestBed.inject(ApplicationRef).tick(); // flush the apply() effect
    return svc;
  }

  beforeEach(() => {
    localStorage.removeItem('auditx.theme');
    document.documentElement.removeAttribute('data-theme');
  });

  afterEach(() => {
    localStorage.removeItem('auditx.theme');
    document.documentElement.removeAttribute('data-theme');
  });

  it('defaults to light and stamps data-theme', () => {
    const svc = make();
    expect(svc.preference()).toBe('light');
    expect(svc.resolved()).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('persists and applies a dark preference', () => {
    const svc = make();
    svc.use('dark');
    TestBed.inject(ApplicationRef).tick();
    expect(svc.preference()).toBe('dark');
    expect(svc.resolved()).toBe('dark');
    expect(localStorage.getItem('auditx.theme')).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('restores the persisted preference on construction', () => {
    localStorage.setItem('auditx.theme', 'dark');
    const svc = make();
    expect(svc.preference()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });
});
