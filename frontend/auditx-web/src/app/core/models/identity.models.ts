/** Account lifecycle states surfaced by the backend. */
export type UserStatus =
  | 'active'
  | 'deactivated'
  | 'awaiting_role_assignment'
  | 'locked';

/** Scope granularity a permission can be constrained to. */
/**
 * Where a permission in a role applies when the role is granted for an org unit: 'global' = the whole organisation,
 * 'org_unit' = only the assigned org unit and the units below it. (Older roles may carry other legacy scope names; the
 * role editor shows those as the whole organisation.)
 */
export type PermissionScopeType = 'global' | 'org_unit';

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
  /** True when a local-password user must change their password before doing anything else. */
  mustChangePassword?: boolean;
}

/** How an administrator establishes a user's initial (or reset) local password. */
export type InitialPasswordMethod = 'set_password' | 'generate_temp' | 'invite';

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ForgotPasswordRequest {
  usernameOrEmail: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface CreateLocalUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  username: string;
  roleNames: string[];
  method: InitialPasswordMethod;
  password: string | null;
}

export interface EnableLocalCredentialRequest {
  username: string;
  method: InitialPasswordMethod;
  password: string | null;
}

export interface AdminResetPasswordRequest {
  method: InitialPasswordMethod;
  password: string | null;
}

/** Result of an admin credential action; generatedPassword is set only for the generate-temp method. */
export interface LocalCredentialResult {
  userId: string;
  username: string;
  generatedPassword: string | null;
}

export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  status: UserStatus;
  lastLoginAt: string | null;
  /** Active role names, for the users-list Role column (present on the list endpoint). */
  roleNames?: string[];
}

export interface UserDetailDto extends UserDto {
  /** Annual audit capacity in person-days (for workload-vs-capacity planning); null when unset. */
  capacityDays: number | null;
  /** Explicit line manager (reporting line); null when none is set (may still resolve via the org-unit head). */
  managerId: string | null;
  managerName: string | null;
  roles: UserRoleDto[];
  delegations: DelegationDto[];
  /** Where this user's credentials live. */
  authenticationSource?: 'directory' | 'local';
  /** Local sign-in username (present for local users). */
  username?: string | null;
}

/** A user's resolved reporting line: the effective manager plus the ordered chain upward. */
export interface ReportingLineNode {
  userId: string;
  displayName: string;
}

export interface ReportingLineDto {
  userId: string;
  /** The effective line manager (explicit manager, else the org-unit head walking up the tree); null if none. */
  lineManagerId: string | null;
  chain: ReportingLineNode[];
}

/** A direct report and their own finding counts, within a manager's team roll-up. */
export interface TeamRollupMember {
  userId: string;
  displayName: string;
  openFindings: number;
  overdueFindings: number;
}

/** Management-line roll-up: totals across a manager's reporting subtree + a per-direct-report breakdown. */
export interface TeamExceptionRollupDto {
  managerId: string;
  reportCount: number;
  openFindings: number;
  overdueFindings: number;
  directReports: TeamRollupMember[];
}

/** Minimal id→name entry from the authenticated-only user directory (GET /users/directory). */
export interface UserDirectoryEntry {
  id: string;
  displayName: string;
}

/** Someone who can be made accountable for an entity: active, not a vendor-support login, with the roles they hold. */
export interface AssignableUser {
  id: string;
  displayName: string;
  email: string;
  hasRole: boolean;
  roles: string[];
}

/** The current user's display preferences: IANA timezone + BCP-47 locale. */
export interface MyPreferences {
  timezone: string;
  locale: string;
}

/** Create a user manually (admin user-management). */
export interface CreateUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  /** Optional AD objectSid to link the directory account; omitted for demo/manual users. */
  externalId?: string | null;
  roleNames?: string[];
}

/** Edit a user's profile fields. */
export interface UpdateUserProfileRequest {
  email: string;
  firstName: string;
  lastName: string;
  displayName?: string | null;
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
  /** 'global' | 'org_unit' (or a legacy scope name on an older role). */
  scopeType: PermissionScopeType | string;
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
  /**
   * False for the retired built-in Auditee role: its abilities are automatic (everyone has auditee access to what
   * they are named on), so it is no longer offered for assignment. Absent/true for every other role.
   */
  isAssignable?: boolean;
}

export interface PermissionDto {
  key: string;
  label: string;
  description: string;
  module: string;
  finestScope: string;
  /** A role may limit this permission to the org unit it is granted for. */
  orgUnitScopable?: boolean;
}

export type MakerCheckerStatus = 'pending' | 'approved' | 'rejected';

export interface MakerCheckerActionDto {
  id: string;
  actionType: string;
  targetObjectType: string;
  targetObjectId: string;
  makerUserId: string;
  /** Display name of whoever raised it (null if it could not be resolved). */
  makerName: string | null;
  /** True when the signed-in user raised it: shown in the queue, but only someone else can decide it. */
  isMine?: boolean;
  status: MakerCheckerStatus;
  createdAt: string;
}

/** Returned (202) when a write is gated behind maker-checker approval. */
export interface PendingActionDto {
  pendingActionId: string;
}

/** Configurable dual-control gate for one enforced action type (US-M1-020). */
export interface MakerCheckerGateDto {
  actionType: string;
  isEnabled: boolean;
  checkerRoleName: string | null;
  allowMakerAsChecker: boolean;
}

export interface ConfigureGateRequest {
  actionType: string;
  isEnabled: boolean;
  checkerRoleName: string | null;
  allowMakerAsChecker: boolean;
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
