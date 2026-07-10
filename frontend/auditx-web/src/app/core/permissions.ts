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
  ViewConfig: 'ViewConfig',
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

  // P0-B — Time tracking
  LogTime: 'LogTime',
  ViewTimeEntries: 'ViewTimeEntries',

  // P1-A — Risk register
  ViewRisk: 'ViewRisk',
  ManageRisk: 'ManageRisk',

  // P1-B — Controls & Compliance registers
  ViewControls: 'ViewControls',
  ManageControls: 'ManageControls',

  // M10 — Notifications
  ConfigureNotifications: 'ConfigureNotifications',

  // M9 — Advanced Analytics & Dashboards
  ViewAnalytics: 'ViewAnalytics',
  ConfigureDashboards: 'ConfigureDashboards',
  PerformanceAnalyticsView: 'PerformanceAnalyticsView',

  // M8 — Reports
  GenerateReport: 'GenerateReport',
  ViewReport: 'ViewReport',
  DistributeReport: 'DistributeReport',
  ConfigureReports: 'ConfigureReports',

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

  // M7 — Sanctions & Disciplinary Grid
  // NOTE: the constant names mirror the milestone spec, but the VALUES must be the
  // backend wire strings (PermissionKeys.cs) — `hasPermission` does a case-sensitive
  // match against the session's permission list. Hence RecordHROutcome / ReferToDC /
  // DCMember keep the backend casing.
  ViewSanctions: 'ViewSanctions',
  TriggerSanctions: 'TriggerSanctions',
  RecommendSanction: 'RecommendSanction',
  RecordHrOutcome: 'RecordHROutcome',
  ReferToDc: 'ReferToDC',
  DcMember: 'DCMember',
  FileAppeal: 'FileAppeal',
  DecideAppeal: 'DecideAppeal',
  ViewGrid: 'ViewGrid',
  ManageGrid: 'ManageGrid',

  // M13 — Audit Committee Workspace
  ACMember: 'ACMember',
  // ACChair already declared in the M3 block above.
  ViewACPacks: 'ViewACPacks',
  GenerateACPack: 'GenerateACPack',
  // CIA already declared in the M6 block above.
} as const;

export type PermissionKey = (typeof Permissions)[keyof typeof Permissions];
