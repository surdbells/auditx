import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateReferenceDataItemRequest,
  ReferenceDataCategory,
  ReferenceDataItem,
  UpdateReferenceDataItemRequest,
} from '../models';

/** Typed client for the managed reference-data endpoints. */
@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private readonly api = inject(ApiService);

  /** The catalogue of editable categories. */
  categories(): Observable<ReferenceDataCategory[]> {
    return this.api.get<ReferenceDataCategory[]>('/reference-data/categories');
  }

  /**
   * Items for a category, ordered by sortOrder then label.
   * Pass `includeInactive` to include archived items (defaults to active-only).
   */
  list(
    category: string,
    includeInactive = false,
  ): Observable<ReferenceDataItem[]> {
    return this.api.get<ReferenceDataItem[]>(`/reference-data/${category}`, {
      includeInactive,
    });
  }

  create(
    category: string,
    body: CreateReferenceDataItemRequest,
  ): Observable<ReferenceDataItem> {
    return this.api.post<ReferenceDataItem>(
      `/reference-data/${category}`,
      body,
    );
  }

  update(
    category: string,
    id: string,
    body: UpdateReferenceDataItemRequest,
  ): Observable<ReferenceDataItem> {
    return this.api.patch<ReferenceDataItem>(
      `/reference-data/${category}/${id}`,
      body,
    );
  }

  archive(category: string, id: string): Observable<void> {
    return this.api.postVoid(`/reference-data/${category}/${id}/archive`);
  }

  reactivate(category: string, id: string): Observable<void> {
    return this.api.postVoid(`/reference-data/${category}/${id}/reactivate`);
  }
}
