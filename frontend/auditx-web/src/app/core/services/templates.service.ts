import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiService } from './api.service';
import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  CloneTemplateRequest,
  CreateSectionRequest,
  CreateTemplateRequest,
  CursorPage,
  PendingActionDto,
  RenameSectionRequest,
  ReorderItemsRequest,
  SaveTemplateItemRequest,
  Template,
  TemplateDiff,
  TemplateListItem,
  TemplateQuery,
  TemplateVersionDetail,
  TemplateVersionSummary,
  UpdateTemplateRequest,
} from '../models';

/**
 * Discriminated result of a publish. The backend may publish directly
 * (200 Template) or queue it for maker-checker approval (202 pendingActionId).
 */
export type PublishResult =
  | { kind: 'published'; template: Template }
  | { kind: 'pending'; pendingActionId: string };

@Injectable({ providedIn: 'root' })
export class TemplatesService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  list(query: TemplateQuery): Observable<CursorPage<TemplateListItem>> {
    return this.api.get<CursorPage<TemplateListItem>>('/templates', {
      auditType: query.auditType,
      status: query.status,
      search: query.search,
      cursor: query.cursor,
      limit: query.limit,
    });
  }

  getById(id: string): Observable<Template> {
    return this.api.get<Template>(`/templates/${id}`);
  }

  create(body: CreateTemplateRequest): Observable<Template> {
    return this.api.post<Template>('/templates', body);
  }

  update(id: string, body: UpdateTemplateRequest): Observable<Template> {
    return this.api.patch<Template>(`/templates/${id}`, body);
  }

  /* ---- Items ---- */

  addItem(id: string, body: SaveTemplateItemRequest): Observable<Template> {
    return this.api.post<Template>(`/templates/${id}/items`, body);
  }

  updateItem(
    id: string,
    itemId: string,
    body: SaveTemplateItemRequest,
  ): Observable<Template> {
    return this.api.patch<Template>(`/templates/${id}/items/${itemId}`, body);
  }

  removeItem(id: string, itemId: string): Observable<void> {
    return this.api.deleteVoid(`/templates/${id}/items/${itemId}`);
  }

  reorderItems(id: string, body: ReorderItemsRequest): Observable<void> {
    return this.api.postVoid(`/templates/${id}/items/reorder`, body);
  }

  /* ---- Sections ---- */

  addSection(id: string, body: CreateSectionRequest): Observable<Template> {
    return this.api.post<Template>(`/templates/${id}/sections`, body);
  }

  renameSection(id: string, body: RenameSectionRequest): Observable<void> {
    return this.api.patchVoid(`/templates/${id}/sections`, body);
  }

  removeSection(id: string, name: string): Observable<void> {
    return this.api.deleteVoid(
      `/templates/${id}/sections?name=${encodeURIComponent(name)}`,
    );
  }

  /* ---- Lifecycle ---- */

  /**
   * Publishes a template. Returns a discriminated result: `published` on a
   * direct 200, or `pending` on a 202 maker-checker gate.
   */
  publish(id: string): Observable<PublishResult> {
    return this.http
      .post<ApiResponse<Template | PendingActionDto>>(
        `${this.baseUrl}/templates/${id}/publish`,
        {},
        { withCredentials: true, observe: 'response' },
      )
      .pipe(map((res) => this.interpretPublish(res)));
  }

  newDraft(id: string): Observable<Template> {
    return this.api.post<Template>(`/templates/${id}/new-draft`);
  }

  archive(id: string): Observable<void> {
    return this.api.postVoid(`/templates/${id}/archive`);
  }

  unarchive(id: string): Observable<void> {
    return this.api.postVoid(`/templates/${id}/unarchive`);
  }

  clone(id: string, body: CloneTemplateRequest): Observable<Template> {
    return this.api.post<Template>(`/templates/${id}/clone`, body);
  }

  /* ---- Versions ---- */

  versions(id: string): Observable<TemplateVersionSummary[]> {
    return this.api.get<TemplateVersionSummary[]>(`/templates/${id}/versions`);
  }

  version(id: string, n: number): Observable<TemplateVersionDetail> {
    return this.api.get<TemplateVersionDetail>(
      `/templates/${id}/versions/${n}`,
    );
  }

  diff(id: string, a: number, b: number): Observable<TemplateDiff> {
    return this.api.get<TemplateDiff>(
      `/templates/${id}/versions/${a}/diff/${b}`,
    );
  }

  private interpretPublish(
    res: HttpResponse<ApiResponse<Template | PendingActionDto>>,
  ): PublishResult {
    const data = res.body?.data;
    if (res.status === 202 || (data && this.isPending(data))) {
      const pending = data as PendingActionDto;
      return { kind: 'pending', pendingActionId: pending.pendingActionId };
    }
    return { kind: 'published', template: data as Template };
  }

  private isPending(
    data: Template | PendingActionDto,
  ): data is PendingActionDto {
    return (data as PendingActionDto).pendingActionId !== undefined;
  }
}
