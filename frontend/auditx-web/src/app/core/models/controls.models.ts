/**
 * P1-B — Internal-controls register + regulation/compliance register + finding links.
 * camelCase on the wire; enums are snake_case strings. Effectiveness is captured when a
 * control is tested (a last-tested date is then required); "not_tested" clears it.
 */

export type ControlType = 'preventive' | 'detective' | 'corrective' | 'directive';

export type ControlFrequency =
  | 'continuous'
  | 'daily'
  | 'weekly'
  | 'monthly'
  | 'quarterly'
  | 'semi_annual'
  | 'annual'
  | 'ad_hoc';

export type ControlEffectiveness =
  | 'not_tested'
  | 'effective'
  | 'partially_effective'
  | 'ineffective';

export const CONTROL_TYPES: readonly ControlType[] = ['preventive', 'detective', 'corrective', 'directive'];
export const CONTROL_FREQUENCIES: readonly ControlFrequency[] = [
  'continuous',
  'daily',
  'weekly',
  'monthly',
  'quarterly',
  'semi_annual',
  'annual',
  'ad_hoc',
];
export const CONTROL_EFFECTIVENESS: readonly ControlEffectiveness[] = [
  'not_tested',
  'effective',
  'partially_effective',
  'ineffective',
];

export interface Control {
  id: string;
  code: string;
  title: string;
  description: string | null;
  controlType: ControlType;
  frequency: ControlFrequency;
  ownerUserId: string;
  auditableEntityId: string | null;
  effectiveness: ControlEffectiveness;
  lastTestedDate: string | null;
  isActive: boolean;
  version: string;
}

export interface ControlListItem {
  id: string;
  code: string;
  title: string;
  controlType: ControlType;
  frequency: ControlFrequency;
  ownerUserId: string;
  effectiveness: ControlEffectiveness;
  lastTestedDate: string | null;
  isActive: boolean;
}

export interface RegisterControlRequest {
  code: string;
  title: string;
  description?: string | null;
  controlType: ControlType;
  frequency: ControlFrequency;
  ownerUserId: string;
  auditableEntityId?: string | null;
}

export interface UpdateControlRequest {
  title: string;
  description?: string | null;
  controlType: ControlType;
  frequency: ControlFrequency;
  ownerUserId: string;
  auditableEntityId?: string | null;
  effectiveness?: ControlEffectiveness | null;
  lastTestedDate?: string | null;
  version: string;
}

export interface SetControlStatusRequest {
  isActive: boolean;
  version: string;
}

export interface ControlQuery {
  type?: ControlType;
  effectiveness?: ControlEffectiveness;
  owner?: string;
  includeRetired?: boolean;
  search?: string;
  cursor?: string | null;
  limit?: number;
}

/* ---- Regulation / compliance register ---- */

export interface Regulation {
  id: string;
  code: string;
  name: string;
  authority: string | null;
  description: string | null;
  category: string | null;
  isActive: boolean;
  version: string;
}

export interface RegulationListItem {
  id: string;
  code: string;
  name: string;
  authority: string | null;
  category: string | null;
  isActive: boolean;
}

export interface RegisterRegulationRequest {
  code: string;
  name: string;
  authority?: string | null;
  description?: string | null;
  category?: string | null;
}

export interface UpdateRegulationRequest {
  name: string;
  authority?: string | null;
  description?: string | null;
  category?: string | null;
  version: string;
}

export interface SetRegulationStatusRequest {
  isActive: boolean;
  version: string;
}

export interface RegulationQuery {
  category?: string;
  includeRetired?: boolean;
  search?: string;
  cursor?: string | null;
  limit?: number;
}

/* ---- Finding <-> control / regulation links ---- */

export interface FindingControlLink {
  linkId: string;
  controlId: string;
  code: string;
  title: string;
  linkedAt: string;
}

export interface FindingRegulationLink {
  linkId: string;
  regulationId: string;
  code: string;
  name: string;
  linkedAt: string;
}

export interface FindingLinks {
  controls: FindingControlLink[];
  regulations: FindingRegulationLink[];
}

export interface LinkControlRequest {
  controlId: string;
}

export interface LinkRegulationRequest {
  regulationId: string;
}
