import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  ConfigureGateRequest,
  MakerCheckerActionDto,
  MakerCheckerGateDto,
  RejectActionRequest,
} from '../models';

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

  /** Admin: the configurable dual-control gates (one per enforced action type). */
  gates(): Observable<MakerCheckerGateDto[]> {
    return this.api.get<MakerCheckerGateDto[]>('/maker-checker/gates');
  }

  /** Admin: enable/disable a gate and set its checker policy. */
  configureGate(body: ConfigureGateRequest): Observable<MakerCheckerGateDto> {
    return this.api.put<MakerCheckerGateDto>('/maker-checker/gates', body);
  }
}
