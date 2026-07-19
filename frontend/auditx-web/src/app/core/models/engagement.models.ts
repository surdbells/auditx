/** A next step the current user can take on an engagement (Engagement Lifecycle module). */
export interface EngagementNextAction {
  code: string;
  label: string;
  /** 'route' → navigate to `route`; 'transition' → advance the audit to `targetState`. */
  kind: 'route' | 'transition';
  route: string | null;
  targetState: string | null;
  permissionKey: string;
}

/** One stage in the lifecycle timeline; state is done | current | pending. */
export interface EngagementStage {
  code: string;
  label: string;
  state: 'done' | 'current' | 'pending';
}

/** The full journey for a single engagement. */
export interface EngagementJourney {
  auditId: string;
  name: string;
  auditType: string;
  status: string;
  stage: string;
  progressPercent: number;
  openExceptionCount: number;
  version: string;
  stages: EngagementStage[];
  nextActions: EngagementNextAction[];
}

/** A board row: an engagement with its stage and the user's next action(s). */
export interface EngagementBoardItem {
  auditId: string;
  name: string;
  auditType: string;
  status: string;
  stage: string;
  progressPercent: number;
  openExceptionCount: number;
  version: string;
  waitingOnMe: boolean;
  nextActions: EngagementNextAction[];
}
