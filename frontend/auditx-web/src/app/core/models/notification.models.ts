/**
 * M10 — Notifications models (camelCase, mirroring the backend contract).
 *
 * Enum-derived string fields are serialized by the backend as snake_case
 * (see `EnumExtensions.ToSnake`): channels as `email` / `sms`, template scope
 * as `system` / `bank`, and dispatch status as `pending` / `dispatched` /
 * `delivered` / `bounced` / `failed` / `dead_letter`.
 */

export type NotificationChannel = 'email' | 'sms';

export type TemplateScope = 'system' | 'institution';

export type DispatchStatus =
  | 'pending'
  | 'dispatched'
  | 'delivered'
  | 'bounced'
  | 'failed'
  | 'dead_letter';

export interface NotificationRule {
  id: string;
  eventType: string;
  name: string;
  recipientResolutionJson: string;
  channelsJson: string;
  templateKey: string;
  isActive: boolean;
  isSystemDefault: boolean;
  /** Opaque base64 row-version token used for optimistic concurrency on update. */
  version: string;
}

export interface NotificationTemplate {
  id: string;
  templateKey: string;
  channel: string;
  scope: TemplateScope;
  subjectTemplate: string | null;
  bodyTemplate: string;
  version: number;
}

export interface NotificationDispatch {
  id: string;
  eventId: string;
  eventType: string;
  ruleId: string | null;
  recipientUserId: string | null;
  recipientAddress: string;
  channel: string;
  templateKey: string;
  templateVersion: number;
  renderedSubject: string | null;
  severity: string | null;
  status: string;
  attempts: number;
  nextRetryAt: string | null;
  deliveredAt: string | null;
  lastError: string | null;
}

export interface RulePreview {
  resolvedRecipientAddresses: string[];
  renderedSubject: string | null;
  renderedBody: string;
}

/** Per-user preferences blob (M1 endpoint); `preferencesJson` may be null. */
export interface NotificationPreferences {
  userId: string;
  preferencesJson: string | null;
}

/* ---- Request payloads ---- */

export interface CreateNotificationRuleRequest {
  eventType: string;
  name: string;
  recipientResolutionJson: string;
  channelsJson: string;
  templateKey: string;
  isActive: boolean;
}

export interface UpdateNotificationRuleRequest {
  name: string;
  recipientResolutionJson: string;
  channelsJson: string;
  templateKey: string;
  isActive: boolean;
  version: string;
}

export interface NotificationTemplateRequest {
  templateKey: string;
  channel: string;
  subjectTemplate: string | null;
  bodyTemplate: string;
}

export interface PreviewNotificationRuleRequest {
  recipientResolutionJson: string;
  templateKey: string;
  samplePayloadJson: string;
}

export interface UpdateNotificationPreferencesRequest {
  preferencesJson: string;
}
