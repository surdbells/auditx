import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import { GlobalSearchResults } from '../models';

/** Typed client for the permission-aware global (header) search. */
@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly api = inject(ApiService);

  /** Cross-module quick search; the backend only returns hits the caller may view. */
  search(q: string): Observable<GlobalSearchResults> {
    return this.api.get<GlobalSearchResults>('/search', { q });
  }
}
