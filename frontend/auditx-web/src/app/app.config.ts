import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideAppInitializer,
  inject,
  isDevMode,
} from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
} from '@angular/router';
import {
  provideHttpClient,
  withInterceptors,
  withFetch,
} from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideServiceWorker } from '@angular/service-worker';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { credentialsInterceptor } from './core/interceptors/credentials.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { AuthService } from './core/services/auth.service';
import { BrandingService } from './core/services/branding.service';
import { TextSizeService } from './core/theme/text-size.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({
        scrollPositionRestoration: 'enabled',
        anchorScrolling: 'enabled',
      }),
    ),
    provideHttpClient(
      withFetch(),
      // credentials first so it is the outermost wrapper, error handling innermost.
      withInterceptors([credentialsInterceptor, errorInterceptor]),
    ),
    provideAnimationsAsync(),
    // Apply the persisted appearance preferences (text size) before first paint to avoid a flash.
    provideAppInitializer(() => {
      inject(TextSizeService);
    }),
    // Probe the existing session on bootstrap so guards have state on first paint.
    provideAppInitializer(async () => {
      const auth = inject(AuthService);
      try {
        await firstValueFrom(auth.loadSession());
      } catch {
        // 401 / network error: app simply starts unauthenticated.
      }
    }),
    // Load the organisation branding (anonymous) so the shell + login are themed before first paint.
    provideAppInitializer(async () => {
      const branding = inject(BrandingService);
      try {
        await firstValueFrom(branding.load());
      } catch {
        // Non-fatal: the app keeps its built-in default theme.
      }
    }),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
