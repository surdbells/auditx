import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { ApiService } from './api.service';
import { LoginRequest, SessionDto } from '../models';

/**
 * Holds the authenticated session as signals and exposes auth operations.
 *
 * The session JWT lives in an HttpOnly cookie that the SPA cannot read, so the
 * source of truth here is the SessionDto returned by `/auth/session` and `/auth/login`.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);

  private readonly _session = signal<SessionDto | null>(null);
  private readonly _sessionLoaded = signal(false);

  /** Current session (null when unauthenticated). */
  readonly session = this._session.asReadonly();

  /** True once an initial session probe has resolved (success or 401). */
  readonly sessionLoaded = this._sessionLoaded.asReadonly();

  readonly isAuthenticated = computed(() => this._session() !== null);

  readonly displayName = computed(() => this._session()?.displayName ?? '');

  readonly status = computed(() => this._session()?.status ?? null);

  /** True when the account exists but has no roles assigned yet. */
  readonly awaitingRole = computed(
    () => this._session()?.status === 'awaiting_role_assignment',
  );

  /** Case-sensitive permission-key check against the session's permission list. */
  hasPermission(key: string): boolean {
    const perms = this._session()?.permissions ?? [];
    return perms.includes(key);
  }

  /** True if the session holds at least one of the supplied permission keys. */
  hasAnyPermission(...keys: string[]): boolean {
    return keys.some((k) => this.hasPermission(k));
  }

  hasRole(role: string): boolean {
    return (this._session()?.roles ?? []).includes(role);
  }

  login(credentials: LoginRequest): Observable<SessionDto> {
    return this.api
      .post<SessionDto>('/auth/login', credentials)
      .pipe(tap((session) => this.setSession(session)));
  }

  /** Windows SSO (Kerberos/IWA). May 401 in dev — caller falls back to forms login. */
  loginWithSso(): Observable<SessionDto> {
    return this.api
      .get<SessionDto>('/auth/sso')
      .pipe(tap((session) => this.setSession(session)));
  }

  logout(): Observable<void> {
    return this.api
      .postVoid('/auth/logout')
      .pipe(tap(() => this.clearSession()));
  }

  /** Loads (or refreshes) the session; marks the session as probed regardless of outcome. */
  loadSession(): Observable<SessionDto> {
    return this.api.get<SessionDto>('/auth/session').pipe(
      tap({
        next: (session) => this.setSession(session),
        error: () => {
          this.clearSession();
          this._sessionLoaded.set(true);
        },
      }),
    );
  }

  setSession(session: SessionDto): void {
    this._session.set(session);
    this._sessionLoaded.set(true);
  }

  clearSession(): void {
    this._session.set(null);
  }
}
