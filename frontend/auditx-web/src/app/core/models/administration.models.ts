/**
 * M15 — Administration console models (camelCase, mirroring the backend contract).
 */

export interface BankSettings {
  bankDisplayName: string;
  timezone: string;
  localeDefault: string;
  adProvisioningFilterOuDn: string | null;
  adProvisioningFilterGroupSid: string | null;
  maxEvidenceFileMb: number;
  maxAuditEvidenceGb: number;
  allowOverlappingPlanPeriods: boolean;
  allowAuditLaunchBeforeApproval: boolean;
}

export interface UpdateBankSettingsRequest {
  bankDisplayName: string;
  timezone: string;
  localeDefault: string;
  adProvisioningFilterOuDn: string | null;
  adProvisioningFilterGroupSid: string | null;
  allowOverlappingPlanPeriods: boolean;
  allowAuditLaunchBeforeApproval: boolean;
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

export interface SystemHealth {
  status: string;
  activeUserCount: number;
  totalUserCount: number;
  templateCount: number;
  integrationCount: number;
  generatedAt: string;
}
