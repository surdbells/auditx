import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateNotificationRuleRequest,
  NotificationDispatch,
  NotificationPreferences,
  NotificationRule,
  NotificationTemplate,
  NotificationTemplateRequest,
  PagedResult,
  PreviewNotificationRuleRequest,
  RulePreview,
  UpdateNotificationPreferencesRequest,
  UpdateNotificationRuleRequest,
} from '../models';

/**
 * Typed client for the M10 Notifications endpoints.
 *
 * Named `NotificationAdminService` to avoid clashing with the existing
 * `NotificationService` toast helper.
 */
@Injectable({ providedIn: 'root' })
export class NotificationAdminService {
  private readonly api = inject(ApiService);

  /* ---- Event catalogue ---- */

  eventCatalogue(): Observable<string[]> {
    return this.api.get<string[]>('/admin/events/catalogue');
  }

  /* ---- Rules ---- */

  listRules(): Observable<NotificationRule[]> {
    return this.api.get<NotificationRule[]>('/notification-rules');
  }

  createRule(
    body: CreateNotificationRuleRequest,
  ): Observable<NotificationRule> {
    return this.api.post<NotificationRule>('/notification-rules', body);
  }

  updateRule(
    id: string,
    body: UpdateNotificationRuleRequest,
  ): Observable<NotificationRule> {
    return this.api.patch<NotificationRule>(`/notification-rules/${id}`, body);
  }

  deactivateRule(id: string): Observable<void> {
    return this.api.deleteVoid(`/notification-rules/${id}`);
  }

  previewRule(body: PreviewNotificationRuleRequest): Observable<RulePreview> {
    return this.api.post<RulePreview>('/notification-rules/preview', body);
  }

  /* ---- Templates ---- */

  listTemplates(): Observable<NotificationTemplate[]> {
    return this.api.get<NotificationTemplate[]>('/notification-templates');
  }

  overrideTemplate(
    body: NotificationTemplateRequest,
  ): Observable<NotificationTemplate> {
    return this.api.post<NotificationTemplate>('/notification-templates', body);
  }

  /* ---- Dispatch log + dead-letter ---- */

  listDispatches(params: {
    status?: string | '';
    eventType?: string | '';
    recipient?: string | '';
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<NotificationDispatch>> {
    return this.api.get<PagedResult<NotificationDispatch>>(
      '/notification-dispatches',
      {
        status: params.status,
        eventType: params.eventType,
        recipient: params.recipient,
        page: params.page,
        pageSize: params.pageSize,
      },
    );
  }

  listDeadLetter(
    page?: number,
    pageSize?: number,
  ): Observable<PagedResult<NotificationDispatch>> {
    return this.api.get<PagedResult<NotificationDispatch>>(
      '/notification-dispatches/dead-letter',
      { page, pageSize },
    );
  }

  retryDispatch(id: string): Observable<NotificationDispatch> {
    return this.api.post<NotificationDispatch>(
      `/notification-dispatches/${id}/retry`,
    );
  }

  /* ---- Per-user preferences (any authenticated user) ---- */

  getMyPreferences(): Observable<NotificationPreferences> {
    return this.api.get<NotificationPreferences>(
      '/users/me/notification-preferences',
    );
  }

  updateMyPreferences(
    body: UpdateNotificationPreferencesRequest,
  ): Observable<void> {
    return this.api.patchVoid('/users/me/notification-preferences', body);
  }
}
