import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models';

export type QueryParams = Record<
  string,
  string | number | boolean | null | undefined
>;

/**
 * Thin, typed wrapper over HttpClient.
 *
 * - Prefixes every request with the configured API base path.
 * - Always sends credentials so the HttpOnly session cookie travels with each call.
 * - Unwraps the standard `{ data, metadata }` envelope, returning `data` directly.
 *
 * Use `*Raw` variants when the caller needs the full envelope (e.g. metadata / pagination)
 * or when an endpoint returns no body (204).
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  get<T>(path: string, params?: QueryParams): Observable<T> {
    return this.getRaw<T>(path, params).pipe(map((r) => r.data));
  }

  getRaw<T>(path: string, params?: QueryParams): Observable<ApiResponse<T>> {
    return this.http.get<ApiResponse<T>>(this.url(path), {
      params: this.toHttpParams(params),
      withCredentials: true,
    });
  }

  post<T>(path: string, body?: unknown): Observable<T> {
    return this.postRaw<T>(path, body).pipe(map((r) => r.data));
  }

  postRaw<T>(path: string, body?: unknown): Observable<ApiResponse<T>> {
    return this.http.post<ApiResponse<T>>(this.url(path), body ?? {}, {
      withCredentials: true,
    });
  }

  patch<T>(path: string, body?: unknown): Observable<T> {
    return this.http
      .patch<ApiResponse<T>>(this.url(path), body ?? {}, {
        withCredentials: true,
      })
      .pipe(map((r) => r.data));
  }

  /** For endpoints that return no body (204). */
  postVoid(path: string, body?: unknown): Observable<void> {
    return this.http.post<void>(this.url(path), body ?? {}, {
      withCredentials: true,
    });
  }

  patchVoid(path: string, body?: unknown): Observable<void> {
    return this.http.patch<void>(this.url(path), body ?? {}, {
      withCredentials: true,
    });
  }

  deleteVoid(path: string): Observable<void> {
    return this.http.delete<void>(this.url(path), { withCredentials: true });
  }

  private url(path: string): string {
    const normalised = path.startsWith('/') ? path : `/${path}`;
    return `${this.baseUrl}${normalised}`;
  }

  private toHttpParams(params?: QueryParams): HttpParams {
    let httpParams = new HttpParams();
    if (!params) {
      return httpParams;
    }
    for (const [key, value] of Object.entries(params)) {
      if (value !== null && value !== undefined && value !== '') {
        httpParams = httpParams.set(key, String(value));
      }
    }
    return httpParams;
  }
}
