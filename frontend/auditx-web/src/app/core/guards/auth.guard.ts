import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';

import { AuthService } from '../services/auth.service';

/**
 * Allows activation only for an authenticated session. If the session has not
 * been probed yet, it loads it first (so guards work on a hard page refresh).
 * Users still awaiting role assignment are routed to the holding page.
 */
export const authGuard: CanActivateFn = (
  _route,
  state,
): boolean | UrlTree | Observable<boolean | UrlTree> => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const resolve = (): boolean | UrlTree => {
    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url },
      });
    }
    if (auth.awaitingRole() && state.url !== '/awaiting-role') {
      return router.createUrlTree(['/awaiting-role']);
    }
    return true;
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
