import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Guarantees `withCredentials` on every outbound request so the HttpOnly session
 * cookie is sent even for calls that did not set it explicitly.
 */
export const credentialsInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req.clone({ withCredentials: true }));
};
