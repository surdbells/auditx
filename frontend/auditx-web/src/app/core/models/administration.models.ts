/**
 * M15 — Administration console models (camelCase, mirroring the backend contract).
 */

export interface InstitutionSettings {
  institutionDisplayName: string;
  timezone: string;
  localeDefault: string;
  adProvisioningFilterOuDn: string | null;
  adProvisioningFilterGroupSid: string | null;
  maxEvidenceFileMb: number;
  maxAuditEvidenceGb: number;
  allowOverlappingPlanPeriods: boolean;
  allowAuditLaunchBeforeApproval: boolean;
  /** When true, an Approved plan's items may still be edited directly (no re-approval); when false, the plan is fully locked and a material revision (re-approval) is the only route to change it. */
  allowMinorPlanRevisionAfterApproval: boolean;
  primaryColor: string;
  accentColor: string;
  logoDataUri: string | null;
  iconDataUri: string | null;
  showOverview: boolean;
  showWalkthrough: boolean;
  /** When true, the walkthrough auto-starts on a user's first visit to each page (per device). */
  autoStartWalkthrough: boolean;
  /** Months a completed report is retained before its artefacts are expired; 0 = retain indefinitely. */
  reportRetentionMonths: number;
  /** Minutes of inactivity before the idle warning appears; 0 = idle logout disabled. */
  idleTimeoutMinutes: number;
  /** Seconds the idle warning counts down before automatic sign-out. */
  idleWarningSeconds: number;
}

export interface UpdateInstitutionSettingsRequest {
  institutionDisplayName: string;
  timezone: string;
  localeDefault: string;
  adProvisioningFilterOuDn: string | null;
  adProvisioningFilterGroupSid: string | null;
  allowOverlappingPlanPeriods: boolean;
  allowAuditLaunchBeforeApproval: boolean;
  allowMinorPlanRevisionAfterApproval: boolean;
  primaryColor: string;
  accentColor: string;
  logoDataUri: string | null;
  iconDataUri: string | null;
  showOverview: boolean;
  showWalkthrough: boolean;
  autoStartWalkthrough: boolean;
  reportRetentionMonths: number;
  idleTimeoutMinutes: number;
  idleWarningSeconds: number;
}

/** Public branding served anonymously from `/branding` — themes the shell before authentication. */
export interface Branding {
  organizationName: string;
  primaryColor: string;
  accentColor: string;
  logoDataUri: string | null;
  iconDataUri: string | null;
  /** Whether the page-guide "Overview" / "Walkthrough" buttons are shown app-wide (admin-configured). */
  showOverview: boolean;
  showWalkthrough: boolean;
  /** Whether the walkthrough auto-starts on first visit to each page (admin-configured). */
  autoStartWalkthrough: boolean;
  /** Idle-logout policy: minutes of inactivity before the warning (0 = disabled) + countdown seconds. */
  idleTimeoutMinutes: number;
  idleWarningSeconds: number;
}

export interface ResourceLimits {
  maxEvidenceFileMb: number;
  maxAuditEvidenceGb: number;
}

export interface BulkOperationError {
  identifier: string;
  message: string;
}

export interface BulkOperationResult {
  successCount: number;
  errors: BulkOperationError[];
}

export interface SupportChannelStatus {
  active: boolean;
  engineerIdentifiers: string[];
  enabledAt: string | null;
  expiresAt: string | null;
  revokedAt: string | null;
}

export interface EnableSupportChannelRequest {
  engineerIdentifiers: string[];
  durationMinutes: number;
}

export type ReleaseInstallStatus = 'Verified' | 'Installed' | 'Rejected';

export interface ReleaseInstall {
  id: string;
  version: string;
  manifestSha256: string;
  changeRecordReference: string;
  status: ReleaseInstallStatus;
  detail: string | null;
  createdAt: string;
}

export interface InstallReleaseRequest {
  version: string;
  manifestSha256: string;
  changeRecordReference: string;
  signatureBase64: string;
  manifestContentBase64: string;
}

export type RestoreDrillOutcome = 'Success' | 'Failed';

export interface RestoreDrill {
  id: string;
  executedAt: string;
  outcome: RestoreDrillOutcome;
  details: string | null;
}

export interface CreateRestoreDrillRequest {
  outcome: RestoreDrillOutcome;
  details: string;
}

export type ObjectRestoreStatus =
  | 'Requested'
  | 'Approved'
  | 'Rejected'
  | 'Executed';

export interface ObjectRestoreRequest {
  id: string;
  objectType: string;
  objectId: string;
  snapshotDate: string;
  justification: string;
  status: ObjectRestoreStatus;
  requestedByUserId: string;
  decidedByUserId: string | null;
  decisionComment: string | null;
}

export interface CreateObjectRestoreRequest {
  objectType: string;
  objectId: string;
  snapshotDate: string;
  justification: string;
}

export interface DecideObjectRestoreRequest {
  approve: boolean;
  comment: string;
}

export interface SystemHealthCheck {
  name: string;
  /** healthy | degraded | unhealthy */
  status: string;
  detail: string | null;
}

export interface SystemHealthMetric {
  label: string;
  value: number;
}

export interface SystemHealth {
  status: string;
  checks: SystemHealthCheck[];
  metrics: SystemHealthMetric[];
  version: string;
  environment: string;
  uptimeSeconds: number;
  generatedAt: string;
}
