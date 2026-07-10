/** Account lifecycle states surfaced by the backend. */
export type UserStatus =
  | 'active'
  | 'deactivated'
  | 'awaiting_role_assignment'
  | 'locked';

/** Scope granularity a permission can be constrained to. */
export type PermissionScopeType = 'global' | 'business_unit' | 'branch' | 'self';

/** Authenticated session view (from `/auth/session` and `/auth/login`). */
export interface SessionDto {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  status: UserStatus;
  roles: string[];
  permissions: string[];
  expiresAt: string;
  absoluteExpiresAt: string;
}

export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  status: UserStatus;
  lastLoginAt: string | null;
}

export interface UserDetailDto extends UserDto {
  /** Annual audit capacity in person-days (for workload-vs-capacity planning); null when unset. */
  capacityDays: number | null;
  roles: UserRoleDto[];
  delegations: DelegationDto[];
}

/** Minimal id→name entry from the authenticated-only user directory (GET /users/directory). */
export interface UserDirectoryEntry {
  id: string;
  displayName: string;
}

export interface UserRoleDto {
  id: string;
  roleId: string;
  roleName: string;
  scopeValue: string | null;
  isDelegation: boolean;
  delegationStart: string | null;
  delegationEnd: string | null;
  isActive: boolean;
}

export interface DelegationDto {
  id: string;
  toUserId: string;
  roleId: string;
  roleName: string;
  fromUserId: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
}

export interface RolePermissionDto {
  key: string;
  scopeType: PermissionScopeType;
  scopePredicateJson: string | null;
}

export interface RoleDto {
  id: string;
  name: string;
  description: string;
  isBuiltIn: boolean;
  isArchived: boolean;
  parentRoleIds: string[];
  permissions: RolePermissionDto[];
}

export interface PermissionDto {
  key: string;
  label: string;
  description: string;
  module: string;
  finestScope: PermissionScopeType;
}

export type MakerCheckerStatus = 'pending' | 'approved' | 'rejected';

export interface MakerCheckerActionDto {
  id: string;
  actionType: string;
  targetObjectType: string;
  targetObjectId: string;
  makerUserId: string;
  makerName: string;
  status: MakerCheckerStatus;
  createdAt: string;
}

/** Returned (202) when a write is gated behind maker-checker approval. */
export interface PendingActionDto {
  pendingActionId: string;
}

/** Request payloads. */
export interface LoginRequest {
  username: string;
  password: string;
}

export interface GrantRoleRequest {
  roleId: string;
  scopeValue: string | null;
}

export interface CreateDelegationRequest {
  toUserId: string;
  roleId: string;
  startDate: string;
  endDate: string;
}

export interface SaveRoleRequest {
  name: string;
  description: string;
  permissions: RolePermissionDto[];
  parentRoleIds: string[];
}

export interface RejectActionRequest {
  reason: string;
}

export interface NotificationPreferencesRequest {
  preferencesJson: string;
}

export interface UserQuery {
  search?: string;
  role?: string;
  status?: UserStatus | '';
  page?: number;
  pageSize?: number;
}
