import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateRatingScaleRequest,
  RatingScale,
  UpdateRatingScaleRequest,
} from '../models';

/** Filter for the `active` query param: `'true'` (default) / `'false'` / `'all'`. */
export type RatingScaleFilter = 'true' | 'false' | 'all';

/** Typed client for the rating-scale configuration endpoints (Rating-type checklist item scoring). */
@Injectable({ providedIn: 'root' })
export class RatingScalesService {
  private readonly api = inject(ApiService);

  list(active: RatingScaleFilter = 'true'): Observable<RatingScale[]> {
    return this.api.get<RatingScale[]>('/rating-scales', { active });
  }

  create(body: CreateRatingScaleRequest): Observable<RatingScale> {
    return this.api.post<RatingScale>('/rating-scales', body);
  }

  update(id: string, body: UpdateRatingScaleRequest): Observable<RatingScale> {
    return this.api.patch<RatingScale>(`/rating-scales/${id}`, body);
  }
}
