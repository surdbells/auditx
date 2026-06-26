import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';
import { NotificationService } from '../services/notification.service';
import { ProblemDetails } from '../models';

/**
 * Surfaces backend errors as Material snackbars and centralises auth handling.
 *
 * - 401: clears the session and redirects to /login (except for the auth probe
 *   endpoints, where a 401 is an expected "not logged in" signal).
 * - RFC 7807 bodies: shows `title` + `detail`, plus any `field_errors`.
 * - The original error is re-thrown so components can still react locally.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const auth = inject(AuthService);
  const notify = inject(NotificationService);

  const isAuthProbe =
    req.url.includes('/auth/session') || req.url.includes('/auth/sso');

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        auth.clearSession();
        if (!isAuthProbe) {
          notify.warning('Your session has expired. Please sign in again.');
          void router.navigate(['/login']);
        }
        return throwError(() => error);
      }

      // Network / CORS failures expose no problem-details body.
      if (error.status === 0) {
        notify.error('Unable to reach the server. Check your connection.');
        return throwError(() => error);
      }

      const problem = extractProblem(error);
      const message = buildMessage(problem, error.status);
      notify.error(message);

      return throwError(() => error);
    }),
  );
};

function extractProblem(error: HttpErrorResponse): ProblemDetails | null {
  const body = error.error as unknown;
  if (body && typeof body === 'object') {
    return body as ProblemDetails;
  }
  return null;
}

function buildMessage(problem: ProblemDetails | null, status: number): string {
  if (!problem) {
    return `Request failed (${status}).`;
  }

  const parts: string[] = [];
  if (problem.title) {
    parts.push(problem.title);
  }
  if (problem.detail) {
    parts.push(problem.detail);
  }

  if (problem.field_errors?.length) {
    const fieldMsgs = problem.field_errors
      .map((f) => `${f.field}: ${f.message}`)
      .join('; ');
    parts.push(fieldMsgs);
  }

  return parts.length ? parts.join(' — ') : `Request failed (${status}).`;
}
