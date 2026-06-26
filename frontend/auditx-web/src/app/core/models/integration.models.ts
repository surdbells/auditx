/**
 * M14 — Integrations & Webhooks models (camelCase, mirroring the backend contract).
 */

export type IntegrationType =
  | 'ActiveDirectory'
  | 'Smtp'
  | 'Sms'
  | 'FileStorage'
  | 'Siem'
  | 'Saml'
  | 'Oidc'
  | 'Webhook';

export const INTEGRATION_TYPES: IntegrationType[] = [
  'ActiveDirectory',
  'Smtp',
  'Sms',
  'FileStorage',
  'Siem',
  'Saml',
  'Oidc',
  'Webhook',
];

export type IntegrationHealthState = 'Healthy' | 'Degraded' | 'Failing';

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
  | 'Pending'
  | 'Delivered'
  | 'Failed'
  | 'DeadLetter';

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
