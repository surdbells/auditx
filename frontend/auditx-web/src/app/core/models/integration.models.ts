/**
 * M14 — Integrations & Webhooks models (camelCase, mirroring the backend contract).
 *
 * Enum-valued fields (`type`, health `state`, delivery `status`) are serialized
 * as snake_case by the backend (`ToSnake`), matching the project-wide contract.
 */

export type IntegrationType =
  | 'active_directory'
  | 'smtp'
  | 'sms'
  | 'file_storage'
  | 'siem'
  | 'saml'
  | 'oidc'
  | 'webhook';

export const INTEGRATION_TYPES: IntegrationType[] = [
  'active_directory',
  'smtp',
  'sms',
  'file_storage',
  'siem',
  'saml',
  'oidc',
  'webhook',
];

export type IntegrationHealthState = 'healthy' | 'degraded' | 'failing';

export interface Integration {
  id: string;
  type: IntegrationType;
  name: string;
  connectionDetailsJson: string;
  hasCredentials: boolean;
  timeoutSeconds: number;
  fallbackIntegrationId: string | null;
  isPrimary: boolean;
  isActive: boolean;
}

export interface IntegrationHealth {
  integrationId: string;
  state: IntegrationHealthState;
  lastSuccessAt: string | null;
  lastFailureAt: string | null;
  recentFailureCount: number;
}

export interface CreateIntegrationRequest {
  type: IntegrationType;
  name: string;
  connectionDetailsJson: string;
  /** Write-only secret material; never returned by the backend. */
  credentials: string | null;
  timeoutSeconds: number;
  fallbackIntegrationId: string | null;
}

export interface UpdateIntegrationRequest {
  name: string;
  connectionDetailsJson: string;
  /** Null/omitted leaves the stored credentials unchanged. */
  credentials: string | null;
  timeoutSeconds: number;
  fallbackIntegrationId: string | null;
  isActive: boolean;
}

export interface IntegrationTestResult {
  success: boolean;
  detail: string;
}

/* ---- Webhooks ---- */

export type WebhookDeliveryStatus =
  | 'pending'
  | 'delivered'
  | 'failed'
  | 'dead_letter';

export interface WebhookSubscription {
  id: string;
  destinationUrl: string;
  subscribedEventTypes: string[];
  isActive: boolean;
}

export interface CreateWebhookSubscriptionRequest {
  destinationUrl: string;
  subscribedEventTypes: string[];
  hmacSecret: string;
  retryPolicyJson: string;
}

export interface WebhookDelivery {
  id: string;
  subscriptionId: string;
  eventType: string;
  eventId: string;
  status: WebhookDeliveryStatus;
  attempts: number;
  nextRetryAt: string | null;
  lastError: string | null;
  deliveredAt: string | null;
  createdAt: string;
}
