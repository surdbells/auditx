import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateDelegationRequest,
  DelegationDto,
  GrantRoleRequest,
  NotificationPreferencesRequest,
  PagedResult,
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

  list(query: UserQuery): Observable<PagedResult<UserDto>> {
    return this.api.get<PagedResult<UserDto>>('/users', {
      search: query.search,
      role: query.role,
      status: query.status,
      page: query.page,
      pageSize: query.pageSize,
    });
  }

  /** Authenticated-only id→name directory (no admin permission required) for resolving user references. */
  directory(query: { page?: number; pageSize?: number } = {}): Observable<PagedResult<UserDirectoryEntry>> {
    return this.api.get<PagedResult<UserDirectoryEntry>>('/users/directory', {
      page: query.page,
      pageSize: query.pageSize,
    });
  }

  getById(id: string): Observable<UserDetailDto> {
    return this.api.get<UserDetailDto>(`/users/${id}`);
  }

  deactivate(id: string): Observable<void> {
    return this.api.patchVoid(`/users/${id}`, { status: 'deactivated' });
  }

  /** Set (or clear, with null) a user's annual audit capacity in person-days. */
  setCapacity(id: string, capacityDays: number | null): Observable<void> {
    return this.api.patchVoid(`/users/${id}/capacity`, { capacityDays });
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
