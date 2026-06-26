import { Provider, EnvironmentProviders } from '@angular/core';
import { provideZonelessChangeDetection } from '@angular/core';
import {
  provideHttpClient,
  withInterceptorsFromDi,
} from '@angular/common/http';
import {
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

/**
 * Common provider set for component/service specs in a zoneless app:
 * zoneless change detection, an HttpClient backed by HttpTestingController,
 * and no-op animations so Material components render synchronously in tests.
 */
export function provideTestEnv(): (Provider | EnvironmentProviders)[] {
  return [
    provideZonelessChangeDetection(),
    provideHttpClient(withInterceptorsFromDi()),
    provideHttpClientTesting(),
    provideNoopAnimations(),
  ];
}
