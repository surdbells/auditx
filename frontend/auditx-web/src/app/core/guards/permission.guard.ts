import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';

import { AuthService } from '../services/auth.service';
import { NotificationService } from '../services/notification.service';

/**
 * Factory producing a guard that requires one of the supplied permission keys.
 * Ensures a session is loaded first, then checks permissions; denies by routing
 * to /dashboard with an explanatory toast.
 */
export function permissionGuard(...keys: string[]): CanActivateFn {
  return (
    _route,
    state,
  ): boolean | UrlTree | Observable<boolean | UrlTree> => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const notify = inject(NotificationService);

    const resolve = (): boolean | UrlTree => {
      if (!auth.isAuthenticated()) {
        return router.createUrlTree(['/login'], {
          queryParams: { returnUrl: state.url },
        });
      }
      if (auth.hasAnyPermission(...keys)) {
        return true;
      }
      notify.warning('You do not have permission to access that area.');
      return router.createUrlTree(['/dashboard']);
    };

    if (auth.sessionLoaded()) {
      return resolve();
    }

    return auth.loadSession().pipe(
      map(() => resolve()),
      catchError(() =>
        of(
          router.createUrlTree(['/login'], {
            queryParams: { returnUrl: state.url },
          }),
        ),
      ),
    );
  };
}
