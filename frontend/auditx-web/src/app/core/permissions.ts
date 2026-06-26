/**
 * Well-known permission keys referenced by route guards and templates.
 * The full catalogue is fetched from the backend at runtime; these are the
 * coarse keys the SPA depends on for navigation gating.
 */
export const Permissions = {
  ManageUsers: 'ManageUsers',
  ManageRoles: 'ManageRoles',
  ApproveMakerChecker: 'ApproveMakerChecker',
  ViewTemplates: 'ViewTemplates',
  ManageTemplates: 'ManageTemplates',
} as const;

export type PermissionKey = (typeof Permissions)[keyof typeof Permissions];
