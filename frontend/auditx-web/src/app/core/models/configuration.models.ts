/**
 * M12 — Template & Workflow Configuration.
 *
 * A configuration domain is versioned: there is at most one ACTIVE version plus a
 * history of inactive drafts/previously-active versions. The persisted
 * `definitionJson` is a raw JSON string; the only editable domain today is
 * `exception_defaults`, whose definition is modelled by
 * {@link ExceptionDefaultsDefinition}.
 */

/** The single editable configuration domain (more may follow). */
export const CONFIG_DOMAIN_EXCEPTION_DEFAULTS = 'exception_defaults';

/** A configuration version as returned by the API (the `{ data }` payload). */
export interface ConfigurationVersion {
  id: string;
  domain: string;
  versionNumber: number;
  /** Raw JSON string; parse with the domain-specific helper. */
  definitionJson: string;
  isActive: boolean;
  changeReason: string | null;
  createdByUserId: string;
  createdAtUtc: string;
  activatedBy?: string | null;
  activatedAt?: string | null;
  /** Optimistic-concurrency rowversion token echoed by the API. */
  version: string;
}

/**
 * Typed projection of the `exception_defaults` definition. Mirrors the persisted
 * snake_case JSON: `{ target_days: { critical, high, medium, low },
 * recurrence_window_months, recurrence_threshold }`.
 */
export interface ExceptionDefaultsDefinition {
  criticalTargetDays: number;
  highTargetDays: number;
  mediumTargetDays: number;
  lowTargetDays: number;
  recurrenceWindowMonths: number;
  recurrenceThreshold: number;
}

/** Wire shape of the `exception_defaults` definition (snake_case). */
export interface ExceptionDefaultsDefinitionJson {
  target_days: {
    critical: number;
    high: number;
    medium: number;
    low: number;
  };
  recurrence_window_months: number;
  recurrence_threshold: number;
}

/** Body for POST /configurations/{domain} (creates a new inactive draft). */
export interface CreateConfigurationVersionRequest {
  definitionJson: string;
  changeReason: string;
}

/** Body for activate / rollback (both require a change reason). */
export interface ConfigurationReasonRequest {
  changeReason: string;
}

/** Body for POST /configurations/{domain}/rollback. */
export interface RollbackConfigurationRequest {
  toVersionNumber: number;
  changeReason: string;
}

/**
 * Result of a maker-checker-gateable configuration action (activate / rollback):
 * either the newly-active version (applied directly, 200) or a `pendingActionId`
 * when gated (202).
 */
export interface ConfigurationActionResult {
  version?: ConfigurationVersion | null;
  pendingActionId?: string | null;
}
