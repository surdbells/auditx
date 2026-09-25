import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateDelegationRequest,
  CreateUserRequest,
  DelegationDto,
  GrantRoleRequest,
  MyPreferences,
  NotificationPreferencesRequest,
  PagedResult,
  ReportingLineDto,
  TeamExceptionRollupDto,
  UpdateUserProfileRequest,
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

  /** The current user's timezone/locale display preferences. */
  myPreferences(): Observable<MyPreferences> {
    return this.api.get<MyPreferences>('/users/me/preferences');
  }

  updateMyPreferences(body: MyPreferences): Observable<void> {
    return this.api.patchVoid('/users/me/preferences', body);
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

  /** Create a user manually (admin). */
  create(body: CreateUserRequest): Observable<UserDto> {
    return this.api.post<UserDto>('/users', body);
  }

  /** Edit a user's profile (name, email, display name). */
  updateProfile(id: string, body: UpdateUserProfileRequest): Observable<void> {
    return this.api.patchVoid(`/users/${id}/profile`, body);
  }

  deactivate(id: string): Observable<void> {
    return this.api.patchVoid(`/users/${id}`, { status: 'deactivated' });
  }

  /** Set (or clear, with null) a user's annual audit capacity in person-days. */
  setCapacity(id: string, capacityDays: number | null): Observable<void> {
    return this.api.patchVoid(`/users/${id}/capacity`, { capacityDays });
  }

  /** Set (or clear, with null) a user's explicit line manager. */
  setManager(id: string, managerId: string | null): Observable<void> {
    return this.api.patchVoid(`/users/${id}/manager`, { managerId });
  }

  /** The user's resolved reporting line (effective manager + chain upward). */
  reportingLine(id: string): Observable<ReportingLineDto> {
    return this.api.get<ReportingLineDto>(`/users/${id}/reporting-line`);
  }

  /** Management-line roll-up: findings across everyone who reports to this user. */
  teamExceptions(id: string): Observable<TeamExceptionRollupDto> {
    return this.api.get<TeamExceptionRollupDto>(`/users/${id}/team-exceptions`);
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
