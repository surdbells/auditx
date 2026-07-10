/**
 * M9 — Advanced Analytics & Dashboards models.
 *
 * JSON is camelCase on the wire (camelCase of the backend C# record properties).
 * Enums (widget type, severity, status) are snake_case strings.
 *
 * Dashboard widget `version` is the dashboard's rowversion (echoed on every
 * widget mutation for optimistic concurrency). CRITICAL: no analytics projection
 * ever carries a sanctions subject identity — sanctions are aggregated by
 * business unit / category only.
 */

/* =========================================================================
 * Dashboards
 * ===================================================================== */

/** The kind of visualisation a widget renders. */
export type WidgetType = 'chart' | 'table' | 'single_metric';

/** A dashboard in the catalogue list (already filtered to what the caller may see). */
export interface DashboardListItem {
  id: string;
  slug: string;
  name: string;
  description?: string | null;
  permissionRequired?: string | null;
  configurationVersion: number;
  widgetCount: number;
}

/**
 * A widget on a dashboard with its computed data payload. `data` is `null` when
 * the metric key is unknown OR the caller may not see that widget (a forbidden
 * widget degrades to null data rather than disappearing).
 */
export interface DashboardWidget {
  id: string;
  widgetType: WidgetType;
  metricKey: string;
  title: string;
  targetRoleId?: string | null;
  position: number;
  configJson?: string | null;
  /** Shape depends on metricKey; see the KPI DTOs below. */
  data: unknown;
  version: string;
}

/** A fully-assembled dashboard: layout + each visible widget's computed data. */
export interface DashboardDetail {
  id: string;
  slug: string;
  name: string;
  description?: string | null;
  permissionRequired?: string | null;
  configurationVersion: number;
  widgets: DashboardWidget[];
  version: string;
}

/* ---- Widget CRUD payloads (ConfigureDashboards) ---- */

/** Body for POST /dashboards/{id}/widgets and PATCH …/{widgetId}. */
export interface SaveDashboardWidgetRequest {
  widgetType: WidgetType;
  metricKey: string;
  title: string;
  targetRoleId?: string | null;
  position: number;
  configJson?: string | null;
  /** The dashboard rowversion from the detail. */
  version: string;
}

/** Body for DELETE /dashboards/{id}/widgets/{widgetId}. */
export interface DeleteDashboardWidgetRequest {
  version: string;
}

/* =========================================================================
 * Analytics KPI DTOs
 * ===================================================================== */

/** Function-performance KPIs: plan execution, in-flight work, exception throughput. */
export interface FunctionPerformance {
  auditsInFlight: number;
  auditsCompleted: number;
  planItemsTotal: number;
  planItemsCompleted: number;
  planExecutionPercent: number;
  openExceptionBacklog: number;
  closedExceptions: number;
  closureRatePercent: number;
}

/** One open-exception count for a severity tier. */
export interface ExceptionSeverityCount {
  severity: string;
  count: number;
}

/** One open-exception count bucketed by age (days since raised). */
export interface ExceptionAgeBucket {
  bucket: string;
  count: number;
}

/** Open exceptions and average closure time for one auditable entity. */
export interface ExceptionByEntity {
  auditableEntityId: string;
  entityName: string;
  openCount: number;
  averageClosureDays: number | null;
}

/** Open exceptions grouped by root-cause taxonomy code (P2-A); "uncategorised" collects blanks. */
export interface ExceptionRootCauseCount {
  rootCauseCategory: string;
  count: number;
}

/** Exception-portfolio KPIs: open by severity / age bucket / entity / root cause + closure time. */
export interface ExceptionPortfolio {
  totalOpen: number;
  bySeverity: ExceptionSeverityCount[];
  byAgeBucket: ExceptionAgeBucket[];
  byEntity: ExceptionByEntity[];
  byRootCause: ExceptionRootCauseCount[];
  averageClosureDays: number | null;
}

/** Sanctions consistency for one business unit (category). NO subject identity. */
export interface SanctionsConsistencyRow {
  businessUnit: string;
  caseCount: number;
  withinGridCount: number;
  gridAdherencePercent: number;
  deviationCount: number;
  appealCount: number;
  appealRatePercent: number;
}

/** Sanctions consistency KPI. Aggregated by business unit; no subject_user_id. */
export interface SanctionsConsistency {
  totalCases: number;
  overallGridAdherencePercent: number;
  overallAppealRatePercent: number;
  byBusinessUnit: SanctionsConsistencyRow[];
}

/** Per-audit-lead performance scorecard: throughput, cycle time, closure metrics. */
export interface PerformanceScorecard {
  auditLeadUserId: string;
  auditsLed: number;
  auditsCompleted: number;
  averageCycleDays: number | null;
  exceptionsRaised: number;
  exceptionsClosed: number;
  averageExceptionClosureDays: number | null;
}

/**
 * Department / business-unit scorecard: audit coverage and exception health
 * aggregated over an org unit AND all of its descendants (subtree roll-up).
 * `depth` is the tree depth (0 = root) for indented rendering.
 */
export interface OrgUnitScorecard {
  orgUnitId: string;
  code: string;
  name: string;
  parentOrgUnitId: string | null;
  depth: number;
  entities: number;
  auditsCompleted: number;
  auditsInFlight: number;
  openFindings: number;
  criticalOpenFindings: number;
  highOpenFindings: number;
  closedFindings: number;
  averageClosureDays: number | null;
}

/** Budget-vs-actual for one audit (P0-B): budgeted hours vs the sum of logged time. */
export interface BudgetVsActualRow {
  auditId: string;
  auditName: string;
  status: string;
  leadUserId: string;
  budgetedHours: number | null;
  actualHours: number;
  varianceHours: number | null;
  percentConsumed: number | null;
}

/** Hours logged in one activity category (utilisation split). */
export interface UtilisationCategory {
  category: string;
  hours: number;
}

/** Total logged effort for one auditor, split by activity category (P0-B). */
export interface UtilisationRow {
  userId: string;
  totalHours: number;
  auditsContributed: number;
  byCategory: UtilisationCategory[];
}

/** One populated cell of the risk heatmap (P1-A). */
export interface RiskHeatmapCell {
  likelihood: number;
  impact: number;
  score: number;
  band: string;
  count: number;
}

/** The enterprise risk heatmap: total open risks + the populated 5×5 cells. */
export interface RiskHeatmap {
  totalOpen: number;
  cells: RiskHeatmapCell[];
}

/** A labelled count in a risk roll-up (band / status / category / strategy). */
export interface RiskCount {
  key: string;
  count: number;
}

/** Risk-register roll-up (P1-A). */
export interface RiskRegisterSummary {
  total: number;
  open: number;
  closed: number;
  overdueReview: number;
  byBand: RiskCount[];
  byStatus: RiskCount[];
  byCategory: RiskCount[];
  byStrategy: RiskCount[];
}

/** A labelled count in a control roll-up (effectiveness / type). */
export interface ControlCount {
  key: string;
  count: number;
}

/** Control-effectiveness roll-up over ACTIVE controls (P1-B). */
export interface ControlEffectivenessSummary {
  totalActive: number;
  tested: number;
  ineffective: number;
  byEffectiveness: ControlCount[];
  byType: ControlCount[];
}

/** Compliance-by-regulation row: one active regulation + its linked/open finding counts (P1-B). */
export interface ComplianceByRegulationRow {
  regulationId: string;
  code: string;
  name: string;
  authority: string | null;
  linkedFindings: number;
  openFindings: number;
}

/** One verification-outcome count for the follow-up summary (P2-B). */
export interface VerificationResultCount {
  result: string;
  count: number;
}

/** Finding follow-up summary (P2-B): management-response coverage, reopen count + verification outcomes. */
export interface FindingFollowUpSummary {
  totalFindings: number;
  closed: number;
  reopened: number;
  withManagementResponse: number;
  verifiedFindings: number;
  byVerificationResult: VerificationResultCount[];
}

/** One procedure-type count for the procedure summary (P2-C). */
export interface ProcedureTypeCount {
  type: string;
  count: number;
}

/** Execution-procedure summary (P2-C): coverage by type + aggregate sampling error-rate. */
export interface ProcedureSummary {
  totalProcedures: number;
  byType: ProcedureTypeCount[];
  samplingProcedures: number;
  totalItemsTested: number;
  totalExceptionsFound: number;
  sampleErrorRatePercent: number | null;
}

/** One document-type count for the evidence summary (P2-D). */
export interface EvidenceTypeCount {
  documentType: string;
  count: number;
}

/** Requested-vs-received evidence summary (P2-D): outstanding / overdue / waived + by document type. */
export interface EvidenceSummary {
  totalRequests: number;
  outstanding: number;
  received: number;
  waived: number;
  overdue: number;
  byDocumentType: EvidenceTypeCount[];
}

/* =========================================================================
 * Metric trend comparison + forecasting (D1)
 * ===================================================================== */

/** The calendar grain a metric series is bucketed into for period-over-period comparison. */
export type ComparisonPeriodType = 'month' | 'quarter' | 'year';

/** One time-series point from the daily snapshot fact table. */
export interface MetricPoint {
  asOfDate: string;
  value: number;
}

/** A KPI time-series (defaults to the trailing 90 days). */
export interface MetricTrend {
  metricKey: string;
  dimension: string | null;
  points: MetricPoint[];
}

/** One calendar period's representative value; `value` is null when the period has no snapshot (a gap). */
export interface MetricPeriodPoint {
  label: string;
  periodStart: string;
  periodEnd: string;
  value: number | null;
}

/** A projected next-period value from an ordinary-least-squares fit over the daily series. */
export interface MetricForecast {
  method: string;
  projectedFor: string;
  projectedValue: number;
  slope: number;
}

/**
 * Period-over-period comparison for a snapshot metric (D1): the trailing period buckets (month / quarter / year),
 * the latest-vs-previous delta + percent change (MoM / QoQ / YoY), and a simple linear forecast of the next period.
 */
export interface MetricComparison {
  metricKey: string;
  dimension: string | null;
  period: string;
  periods: MetricPeriodPoint[];
  current: number | null;
  previous: number | null;
  delta: number | null;
  percentChange: number | null;
  forecast: MetricForecast | null;
}

/** A single Critical/High open exception for the material-findings widget. */
export interface MaterialFinding {
  exceptionId: string;
  auditId: string;
  title: string;
  severity: string;
  status: string;
  auditableEntityId: string | null;
  raisedAt: string;
  targetDate: string;
}

/**
 * Annual-plan execution status — counts of plan items by lifecycle state + %.
 * Named `PlanStatusKpi` to avoid clashing with the M3 plan-lifecycle
 * `PlanStatus` string-union in planning.models.ts.
 */
export interface PlanStatusKpi {
  totalPlans: number;
  totalItems: number;
  planned: number;
  inProgress: number;
  completed: number;
  deferred: number;
  completionPercent: number;
}

/* =========================================================================
 * Recurrence clusters
 * ===================================================================== */

/** A detected recurrence cluster (list surface). */
export interface RecurrenceCluster {
  id: string;
  auditableEntityId: string;
  category?: string | null;
  closedExceptionCount: number;
  windowMonths: number;
  firstOccurredAt: string;
  lastOccurredAt: string;
  detectedAt: string;
  notifiedAt?: string | null;
}

/** One member exception inside a recurrence cluster. */
export interface RecurrenceClusterMember {
  exceptionId: string;
  auditId: string;
  title: string;
  severity: string;
  status: string;
  raisedAt: string;
  closedAt?: string | null;
}

/** A recurrence cluster with its member exceptions (drilldown). */
export interface RecurrenceClusterDetail extends RecurrenceCluster {
  members: RecurrenceClusterMember[];
}
