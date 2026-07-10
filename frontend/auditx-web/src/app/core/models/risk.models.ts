/**
 * P1-A — Enterprise risk-register models. Ratings are 1–5; score = likelihood×impact (1–25);
 * band is derived from the score. The "current" position is the residual (once assessed) else inherent.
 * camelCase on the wire; enums are snake_case strings.
 */

export type RiskStatus = 'open' | 'assessed' | 'mitigating' | 'monitoring' | 'closed';

export type RiskTreatmentStrategy = 'accept' | 'mitigate' | 'transfer' | 'avoid';

export type RiskBand = 'low' | 'medium' | 'high' | 'critical';

export const RISK_STATUSES: readonly RiskStatus[] = ['open', 'assessed', 'mitigating', 'monitoring', 'closed'];
export const RISK_STRATEGIES: readonly RiskTreatmentStrategy[] = ['accept', 'mitigate', 'transfer', 'avoid'];
export const RISK_BANDS: readonly RiskBand[] = ['low', 'medium', 'high', 'critical'];

export interface Risk {
  id: string;
  title: string;
  description: string | null;
  category: string;
  ownerUserId: string;
  auditableEntityId: string | null;
  inherentLikelihood: number;
  inherentImpact: number;
  inherentScore: number;
  inherentBand: RiskBand;
  residualLikelihood: number | null;
  residualImpact: number | null;
  residualScore: number | null;
  currentLikelihood: number;
  currentImpact: number;
  currentScore: number;
  currentBand: RiskBand;
  treatmentStrategy: RiskTreatmentStrategy | null;
  treatmentPlan: string | null;
  status: RiskStatus;
  targetDate: string | null;
  nextReviewDate: string | null;
  identifiedAt: string;
  identifiedByUserId: string;
  closureRationale: string | null;
  version: string;
}

export interface RiskListItem {
  id: string;
  title: string;
  category: string;
  ownerUserId: string;
  status: RiskStatus;
  currentLikelihood: number;
  currentImpact: number;
  currentScore: number;
  currentBand: RiskBand;
  treatmentStrategy: RiskTreatmentStrategy | null;
  targetDate: string | null;
  nextReviewDate: string | null;
}

/* ---- Request payloads ---- */

export interface RegisterRiskRequest {
  title: string;
  description?: string | null;
  category: string;
  ownerUserId: string;
  auditableEntityId?: string | null;
  inherentLikelihood: number;
  inherentImpact: number;
  targetDate?: string | null;
}

export interface UpdateRiskRequest {
  title: string;
  description?: string | null;
  category: string;
  ownerUserId: string;
  auditableEntityId?: string | null;
  inherentLikelihood: number;
  inherentImpact: number;
  residualLikelihood?: number | null;
  residualImpact?: number | null;
  treatmentStrategy?: RiskTreatmentStrategy | null;
  treatmentPlan?: string | null;
  targetDate?: string | null;
  nextReviewDate?: string | null;
  version: string;
}

export interface ChangeRiskStatusRequest {
  status: RiskStatus;
  rationale?: string | null;
  version: string;
}

export interface RiskQuery {
  status?: RiskStatus;
  category?: string;
  owner?: string;
  band?: RiskBand;
  includeClosed?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}
