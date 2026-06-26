import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  AuditTrailEntryView,
  BulkImportRequest,
  BulkImportResult,
  CreateEntityRequest,
  CursorPage,
  Entity,
  EntityListItem,
  EntityQuery,
  SaveRiskScoresRequest,
  UpdateEntityRequest,
} from '../models';

/** Typed client for the M3 Audit Universe endpoints. */
@Injectable({ providedIn: 'root' })
export class UniverseService {
  private readonly api = inject(ApiService);

  list(query: EntityQuery): Observable<CursorPage<EntityListItem>> {
    return this.api.get<CursorPage<EntityListItem>>('/audit-universe/entities', {
      entityType: query.entityType,
      owner: query.owner,
      archived: query.archived,
      search: query.search,
      cursor: query.cursor,
      limit: query.limit,
    });
  }

  getById(id: string): Observable<Entity> {
    return this.api.get<Entity>(`/audit-universe/entities/${id}`);
  }

  create(body: CreateEntityRequest): Observable<Entity> {
    return this.api.post<Entity>('/audit-universe/entities', body);
  }

  update(id: string, body: UpdateEntityRequest): Observable<Entity> {
    return this.api.patch<Entity>(`/audit-universe/entities/${id}`, body);
  }

  /** Archives the entity (DELETE → 204). */
  archive(id: string): Observable<void> {
    return this.api.deleteVoid(`/audit-universe/entities/${id}`);
  }

  saveRiskScores(
    id: string,
    body: SaveRiskScoresRequest,
  ): Observable<Entity> {
    return this.api.post<Entity>(
      `/audit-universe/entities/${id}/risk-scores`,
      body,
    );
  }

  riskScoreHistory(id: string): Observable<AuditTrailEntryView[]> {
    return this.api.get<AuditTrailEntryView[]>(
      `/audit-universe/entities/${id}/risk-scores/history`,
    );
  }

  bulkImport(body: BulkImportRequest): Observable<BulkImportResult> {
    return this.api.post<BulkImportResult>(
      '/audit-universe/entities/bulk-import',
      body,
    );
  }

  /* ---- Entity types ---- */

  entityTypes(): Observable<string[]> {
    return this.api.get<string[]>('/audit-universe/entity-types');
  }

  createEntityType(type: string): Observable<void> {
    return this.api.postVoid('/audit-universe/entity-types', { type });
  }

  deleteEntityType(type: string): Observable<void> {
    return this.api.deleteVoid(
      `/audit-universe/entity-types/${encodeURIComponent(type)}`,
    );
  }
}
