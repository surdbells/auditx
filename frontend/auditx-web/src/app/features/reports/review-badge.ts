import { ReportSignOffStatus, ReportStage } from '../../core/models';

/** The one badge that says where a report version stands: a draft, or a final and its sign-off. */
export interface ReviewBadge {
  /** A `.status-badge[data-status]` colour key. */
  status: string;
  /** The i18n key of the label. */
  labelKey: string;
}

export function reviewBadge(report: { stage?: ReportStage | null; signOffStatus?: ReportSignOffStatus | null }): ReviewBadge {
  if (report.stage === 'draft') {
    return { status: 'draft', labelKey: 'reports.stage.draft' };
  }
  switch (report.signOffStatus) {
    case 'awaiting':
      return { status: 'pending', labelKey: 'reports.stage.awaiting' };
    case 'signed_off':
      return { status: 'approved', labelKey: 'reports.stage.signedOff' };
    case 'returned':
      return { status: 'revisions_requested', labelKey: 'reports.stage.returned' };
    default:
      // A final report issued before sign-off existed.
      return { status: 'completed', labelKey: 'reports.stage.final' };
  }
}
