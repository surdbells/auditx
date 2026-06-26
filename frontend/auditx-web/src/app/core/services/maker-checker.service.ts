import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import { MakerCheckerActionDto, RejectActionRequest } from '../models';

@Injectable({ providedIn: 'root' })
export class MakerCheckerService {
  private readonly api = inject(ApiService);

  pending(actionType?: string): Observable<MakerCheckerActionDto[]> {
    return this.api.get<MakerCheckerActionDto[]>('/maker-checker/pending', {
      actionType,
    });
  }

  approve(id: string): Observable<void> {
    return this.api.postVoid(`/maker-checker/${id}/approve`);
  }

  reject(id: string, body: RejectActionRequest): Observable<void> {
    return this.api.postVoid(`/maker-checker/${id}/reject`, body);
  }
}
