import { StandaloneReportKind } from './report.models';

/** How often a report schedule auto-generates its report (D3-C). */
export type ReportCadence = 'daily' | 'weekly' | 'monthly' | 'quarterly';

export const REPORT_CADENCES: ReportCadence[] = ['daily', 'weekly', 'monthly', 'quarterly'];

/** A recurring report schedule. Recipients are resolved lists (directory users + ad-hoc emails). */
export interface ReportSchedule {
  id: string;
  name: string;
  kind: StandaloneReportKind;
  cadence: ReportCadence;
  recipientUserIds: string[];
  recipientEmails: string[];
  isActive: boolean;
  nextRunAt: string;
  lastRunAt: string | null;
  lastReportId: string | null;
  createdByUserId: string;
  version: string;
}

export interface CreateReportScheduleRequest {
  name: string;
  kind: StandaloneReportKind;
  cadence: ReportCadence;
  recipientUserIds?: string[];
  recipientEmails?: string[];
}

export interface UpdateReportScheduleRequest {
  name: string;
  cadence: ReportCadence;
  recipientUserIds?: string[];
  recipientEmails?: string[];
  isActive: boolean;
  version: string;
}
