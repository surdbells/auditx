import { HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiService } from './api.service';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import {
  ApiResponse,
  PendingActionDto,
  PermissionDto,
  RoleDto,
  SaveRoleRequest,
} from '../models';

/**
 * Discriminated result of a role write. The backend may complete the change
 * directly (201/200 RoleDto) or queue it for maker-checker approval (202).
 */
export type RoleSaveResult =
  | { kind: 'saved'; role: RoleDto }
  | { kind: 'pending'; pendingActionId: string };

@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  list(includeArchived = false): Observable<RoleDto[]> {
    return this.api.get<RoleDto[]>('/roles', { includeArchived });
  }

  getById(id: string): Observable<RoleDto> {
    return this.api.get<RoleDto>(`/roles/${id}`);
  }

  permissionsCatalogue(): Observable<PermissionDto[]> {
    return this.api.get<PermissionDto[]>('/permissions/catalogue');
  }

  archive(id: string): Observable<void> {
    return this.api.postVoid(`/roles/${id}/archive`);
  }

  create(body: SaveRoleRequest): Observable<RoleSaveResult> {
    return this.http
      .post<ApiResponse<RoleDto | PendingActionDto>>(
        `${this.baseUrl}/roles`,
        body,
        { withCredentials: true, observe: 'response' },
      )
      .pipe(map((res) => this.interpret(res)));
  }

  update(id: string, body: SaveRoleRequest): Observable<RoleSaveResult> {
    return this.http
      .patch<ApiResponse<RoleDto | PendingActionDto>>(
        `${this.baseUrl}/roles/${id}`,
        body,
        { withCredentials: true, observe: 'response' },
      )
      .pipe(map((res) => this.interpret(res)));
  }

  private interpret(
    res: HttpResponse<ApiResponse<RoleDto | PendingActionDto>>,
  ): RoleSaveResult {
    const data = res.body?.data;
    if (res.status === 202 || (data && this.isPending(data))) {
      const pending = data as PendingActionDto;
      return { kind: 'pending', pendingActionId: pending.pendingActionId };
    }
    return { kind: 'saved', role: data as RoleDto };
  }

  private isPending(
    data: RoleDto | PendingActionDto,
  ): data is PendingActionDto {
    return (data as PendingActionDto).pendingActionId !== undefined;
  }
}
