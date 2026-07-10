/**
 * P2-C — typed fieldwork procedures (sampling / interview / walkthrough). camelCase on the wire;
 * enums are snake_case strings. Sampling numerics + method are null for interviews and walkthroughs.
 */

export type ProcedureType = 'sampling' | 'interview' | 'walkthrough';

export type SamplingMethod = 'random' | 'systematic' | 'judgmental' | 'haphazard';

export const PROCEDURE_TYPES: readonly ProcedureType[] = ['sampling', 'interview', 'walkthrough'];
export const SAMPLING_METHODS: readonly SamplingMethod[] = ['random', 'systematic', 'judgmental', 'haphazard'];

export interface AuditProcedure {
  id: string;
  auditId: string;
  checklistItemId: string | null;
  type: ProcedureType;
  performedByUserId: string;
  performedOn: string;
  summary: string;
  counterparty: string | null;
  population: number | null;
  sampleSize: number | null;
  itemsTested: number | null;
  exceptionsFound: number | null;
  method: SamplingMethod | null;
  version: string;
}

export interface RecordProcedureRequest {
  type: ProcedureType;
  checklistItemId?: string | null;
  performedOn: string;
  summary: string;
  counterparty?: string | null;
  population?: number | null;
  sampleSize?: number | null;
  itemsTested?: number | null;
  exceptionsFound?: number | null;
  method?: SamplingMethod | null;
}
