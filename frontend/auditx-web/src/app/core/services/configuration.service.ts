import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  ApiResponse,
  ConfigurationActionResult,
  ConfigurationReasonRequest,
  ConfigurationVersion,
  CreateConfigurationVersionRequest,
  CursorPage,
  ExceptionDefaultsDefinition,
  ExceptionDefaultsDefinitionJson,
  PendingActionDto,
  RollbackConfigurationRequest,
} from '../models';

/**
 * Typed client for the M12 Configuration endpoints (base `/configurations`).
 *
 * Reads need ViewConfig; writes need ManageConfiguration. Activate and rollback
 * are maker-checker-gateable: a 200 carries the new active {@link
 * ConfigurationVersion}, a 202 carries only `{ pendingActionId }`. {@link
 * activateVersion} / {@link rollback} read the HTTP status to disambiguate
 * (mirrors {@link SanctionsService.activateGrid}).
 *
 * The `exception_defaults` definition is persisted as a JSON string; this service
 * parses/serialises it to/from the typed {@link ExceptionDefaultsDefinition}.
 */
@Injectable({ providedIn: 'root' })
export class ConfigurationService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /** The currently active version for a domain. */
  getActive(domain: string): Observable<ConfigurationVersion> {
    return this.api.get<ConfigurationVersion>(`/configurations/${domain}`);
  }

  /** Cursor-paged version history (newest first). */
  listVersions(
    domain: string,
    cursor?: string | null,
    limit?: number,
  ): Observable<CursorPage<ConfigurationVersion>> {
    return this.api.get<CursorPage<ConfigurationVersion>>(
      `/configurations/${domain}/versions`,
      { cursor, limit },
    );
  }

  /** Creates a new inactive draft for a domain. */
  createDraft(
    domain: string,
    body: CreateConfigurationVersionRequest,
  ): Observable<ConfigurationVersion> {
    return this.api.post<ConfigurationVersion>(
      `/configurations/${domain}`,
      body,
    );
  }

  /**
   * Activates a version. Returns `{ version }` when applied directly (200) or
   * `{ pendingActionId }` when maker-checker-gated (202).
   */
  activateVersion(
    domain: string,
    versionNumber: number,
    body: ConfigurationReasonRequest,
  ): Observable<ConfigurationActionResult> {
    return this.gatedAction(
      this.http.post<ApiResponse<ConfigurationVersion | PendingActionDto>>(
        `${this.baseUrl}/configurations/${domain}/versions/${versionNumber}/activate`,
        body,
        { withCredentials: true, observe: 'response' },
      ),
    );
  }

  /**
   * Rolls back to an earlier version. Returns `{ version }` when applied directly
   * (200) or `{ pendingActionId }` when gated (202). A 409 (rolling back to the
   * already-active version) surfaces to the caller's error handler.
   */
  rollback(
    domain: string,
    body: RollbackConfigurationRequest,
  ): Observable<ConfigurationActionResult> {
    return this.gatedAction(
      this.http.post<ApiResponse<ConfigurationVersion | PendingActionDto>>(
        `${this.baseUrl}/configurations/${domain}/rollback`,
        body,
        { withCredentials: true, observe: 'response' },
      ),
    );
  }

  private gatedAction(
    op: Observable<
      HttpResponse<ApiResponse<ConfigurationVersion | PendingActionDto>>
    >,
  ): Observable<ConfigurationActionResult> {
    return op.pipe(
      map((res) => {
        const data = res.body?.data;
        if (res.status === 202 || (data && this.isPending(data))) {
          const pending = data as PendingActionDto | null;
          return { pendingActionId: pending?.pendingActionId };
        }
        return { version: data as ConfigurationVersion };
      }),
    );
  }

  private isPending(
    data: ConfigurationVersion | PendingActionDto,
  ): data is PendingActionDto {
    return (data as PendingActionDto).pendingActionId !== undefined;
  }

  /* ---- exception_defaults (de)serialisation ---- */

  /** Parses an `exception_defaults` definition JSON string to the typed model. */
  parseExceptionDefaults(
    definitionJson: string,
  ): ExceptionDefaultsDefinition | null {
    let parsed: ExceptionDefaultsDefinitionJson;
    try {
      parsed = JSON.parse(definitionJson) as ExceptionDefaultsDefinitionJson;
    } catch {
      return null;
    }
    const td = parsed.target_days;
    if (!td) {
      return null;
    }
    return {
      criticalTargetDays: td.critical,
      highTargetDays: td.high,
      mediumTargetDays: td.medium,
      lowTargetDays: td.low,
      recurrenceWindowMonths: parsed.recurrence_window_months,
      recurrenceThreshold: parsed.recurrence_threshold,
    };
  }

  /** Serialises the typed model back to the persisted snake_case JSON string. */
  serialiseExceptionDefaults(model: ExceptionDefaultsDefinition): string {
    const json: ExceptionDefaultsDefinitionJson = {
      target_days: {
        critical: model.criticalTargetDays,
        high: model.highTargetDays,
        medium: model.mediumTargetDays,
        low: model.lowTargetDays,
      },
      recurrence_window_months: model.recurrenceWindowMonths,
      recurrence_threshold: model.recurrenceThreshold,
    };
    return JSON.stringify(json);
  }
}
