import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  AddPlanItemRequest,
  CursorPage,
  Plan,
  PlanDecisionRequest,
  PlanExecution,
  PlanItem,
  PlanListItem,
  PlanQuery,
  ReorderPlanItemsRequest,
  SavePlanRequest,
  SubmitRevisionRequest,
} from '../models';

/** Typed client for the M3 Annual Plans endpoints. */
@Injectable({ providedIn: 'root' })
export class AnnualPlansService {
  private readonly api = inject(ApiService);

  list(query: PlanQuery): Observable<CursorPage<PlanListItem>> {
    return this.api.get<CursorPage<PlanListItem>>('/annual-plans', {
      status: query.status,
      cursor: query.cursor,
      limit: query.limit,
    });
  }

  getById(id: string): Observable<Plan> {
    return this.api.get<Plan>(`/annual-plans/${id}`);
  }

  execution(id: string): Observable<PlanExecution> {
    return this.api.get<PlanExecution>(`/annual-plans/${id}/execution`);
  }

  create(body: SavePlanRequest): Observable<Plan> {
    return this.api.post<Plan>('/annual-plans', body);
  }

  update(id: string, body: SavePlanRequest): Observable<Plan> {
    return this.api.patch<Plan>(`/annual-plans/${id}`, body);
  }

  /* ---- Items ---- */

  addItem(id: string, body: AddPlanItemRequest): Observable<PlanItem> {
    return this.api.post<PlanItem>(`/annual-plans/${id}/items`, body);
  }

  removeItem(id: string, itemId: string): Observable<void> {
    return this.api.deleteVoid(`/annual-plans/${id}/items/${itemId}`);
  }

  reorderItems(id: string, body: ReorderPlanItemsRequest): Observable<void> {
    return this.api.postVoid(`/annual-plans/${id}/items/reorder`, body);
  }

  /* ---- Lifecycle ---- */

  submit(id: string): Observable<Plan> {
    return this.api.post<Plan>(`/annual-plans/${id}/submit`);
  }

  submitRevision(
    id: string,
    body: SubmitRevisionRequest,
  ): Observable<Plan> {
    return this.api.post<Plan>(`/annual-plans/${id}/submit-revision`, body);
  }

  decision(id: string, body: PlanDecisionRequest): Observable<Plan> {
    return this.api.post<Plan>(`/annual-plans/${id}/decision`, body);
  }

  close(id: string): Observable<void> {
    return this.api.postVoid(`/annual-plans/${id}/close`);
  }
}
