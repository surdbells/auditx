import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateSavedViewRequest,
  SavedView,
  UpdateSavedViewRequest,
} from '../models';

/**
 * Saved views (D3-A): a user's named filter/parameter sets per screen, plus shared views. Owner-scoped server-side;
 * these endpoints are authentication-gated only (no business permission).
 */
@Injectable({ providedIn: 'root' })
export class SavedViewsService {
  private readonly api = inject(ApiService);

  /** The caller's own + shared views for a screen (owned first, then by name). */
  list(viewKey: string): Observable<SavedView[]> {
    return this.api.get<SavedView[]>('/saved-views', { viewKey });
  }

  create(body: CreateSavedViewRequest): Observable<SavedView> {
    return this.api.post<SavedView>('/saved-views', body);
  }

  update(id: string, body: UpdateSavedViewRequest): Observable<SavedView> {
    return this.api.patch<SavedView>(`/saved-views/${id}`, body);
  }

  delete(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/saved-views/${id}?version=${encodeURIComponent(version)}`);
  }
}
