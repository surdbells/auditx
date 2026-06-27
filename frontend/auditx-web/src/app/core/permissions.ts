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

  // M3 — Audit Universe & Planning
  ViewUniverse: 'ViewUniverse',
  ViewPlan: 'ViewPlan',
  ViewCoverage: 'ViewCoverage',
  ManageUniverse: 'ManageUniverse',
  ScoreRisk: 'ScoreRisk',
  ManagePlan: 'ManagePlan',
  ManageConfiguration: 'ManageConfiguration',
  ACChair: 'ACChair',

  // M4 — Audit Lifecycle
  ViewAudits: 'ViewAudits',
  ViewAudit: 'ViewAudit',
  CreateAudit: 'CreateAudit',
  ManageAudit: 'ManageAudit',

  // M5 — Audit Execution / Fieldwork
  RespondItem: 'RespondItem',
  UploadEvidence: 'UploadEvidence',
  ViewEvidence: 'ViewEvidence',
  ManageEvidence: 'ManageEvidence',

  // M10 — Notifications
  ConfigureNotifications: 'ConfigureNotifications',

  // M6 — Exceptions & Management Action Plans (MAP)
  ViewExceptions: 'ViewExceptions',
  RaiseException: 'RaiseException',
  ManageException: 'ManageException',
  SubmitMap: 'SubmitMap',
  ApproveMap: 'ApproveMap',
  CloseException: 'CloseException',
  CancelException: 'CancelException',
  CIA: 'CIA',

  // M11 — Audit Trail & Evidence Integrity
  ViewAuditTrail: 'ViewAuditTrail',
  ExportAuditTrail: 'ExportAuditTrail',
} as const;

export type PermissionKey = (typeof Permissions)[keyof typeof Permissions];
