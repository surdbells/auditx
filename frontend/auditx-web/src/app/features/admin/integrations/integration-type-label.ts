import { IntegrationType } from '../../../core/models';

/** Display labels for the snake_case integration types emitted by the backend. */
const LABELS: Record<IntegrationType, string> = {
  active_directory: 'Active Directory',
  smtp: 'SMTP',
  sms: 'SMS',
  file_storage: 'File storage',
  siem: 'SIEM',
  saml: 'SAML',
  oidc: 'OIDC',
  webhook: 'Webhook',
};

/** Renders an integration type for display; unknown values fall back to the raw string. */
export function humaniseIntegrationType(type: string): string {
  return LABELS[type as IntegrationType] ?? type;
}
