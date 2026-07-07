import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateDelegationRequest,
  CursorPage,
  DelegationDto,
  GrantRoleRequest,
  NotificationPreferencesRequest,
  UserDetailDto,
  UserDirectoryEntry,
  UserDto,
  UserQuery,
  UserRoleDto,
} from '../models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly api = inject(ApiService);

  me(): Observable<UserDto> {
    return this.api.get<UserDto>('/users/me');
  }

  updateMyNotificationPreferences(
    body: NotificationPreferencesRequest,
  ): Observable<void> {
    return this.api.patchVoid('/users/me/notification-preferences', body);
  }

  list(query: UserQuery): Observable<CursorPage<UserDto>> {
    return this.api.get<CursorPage<UserDto>>('/users', {
      search: query.search,
      role: query.role,
      status: query.status,
      cursor: query.cursor,
      limit: query.limit,
    });
  }

  /** Authenticated-only id→name directory (no admin permission required) for resolving user references. */
  directory(query: { cursor?: string | null; limit?: number } = {}): Observable<CursorPage<UserDirectoryEntry>> {
    return this.api.get<CursorPage<UserDirectoryEntry>>('/users/directory', {
      cursor: query.cursor,
      limit: query.limit,
    });
  }

  getById(id: string): Observable<UserDetailDto> {
    return this.api.get<UserDetailDto>(`/users/${id}`);
  }

  deactivate(id: string): Observable<void> {
    return this.api.patchVoid(`/users/${id}`, { status: 'deactivated' });
  }

  grantRole(userId: string, body: GrantRoleRequest): Observable<UserRoleDto> {
    return this.api.post<UserRoleDto>(`/users/${userId}/roles`, body);
  }

  revokeRole(userId: string, userRoleId: string): Observable<void> {
    return this.api.deleteVoid(`/users/${userId}/roles/${userRoleId}`);
  }

  createDelegation(
    userId: string,
    body: CreateDelegationRequest,
  ): Observable<DelegationDto> {
    return this.api.post<DelegationDto>(`/users/${userId}/delegations`, body);
  }

  revokeDelegation(userId: string, delegationId: string): Observable<void> {
    return this.api.deleteVoid(`/users/${userId}/delegations/${delegationId}`);
  }
}
