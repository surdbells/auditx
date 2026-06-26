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

  // M14 — Integrations & Webhooks
  ViewIntegrations: 'ViewIntegrations',
  ViewIntegrationHealth: 'ViewIntegrationHealth',
  ConfigureIntegrations: 'ConfigureIntegrations',
  ConfigureWebhooks: 'ConfigureWebhooks',
  AdminOps: 'AdminOps',

  // M15 — Administration
  ViewBankSettings: 'ViewBankSettings',
  ViewSystemHealth: 'ViewSystemHealth',
  ManageBankSettings: 'ManageBankSettings',
  ConfigureLimits: 'ConfigureLimits',
  ManageSupportChannel: 'ManageSupportChannel',
  InstallReleases: 'InstallReleases',
  ManageRetention: 'ManageRetention',
  ExecRestore: 'ExecRestore',
} as const;

export type PermissionKey = (typeof Permissions)[keyof typeof Permissions];
