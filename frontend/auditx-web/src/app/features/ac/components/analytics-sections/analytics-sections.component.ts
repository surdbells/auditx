import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import {
  AcMaterialFinding,
  AcRecurrenceCluster,
  AcSanctionsConsistencyRow,
  AcSeverityCount,
} from '../../../../core/models';
import { days, humanise, percent } from '../../format';

/**
 * Renders the shared AC analytics snapshot sections — plan status, exception
 * portfolio, material findings, sanctions consistency (aggregate, no subject
 * identity) and recurrence clusters. Used by both the pack viewer (immutable
 * snapshot) and the read-only AC dashboard (live aggregates).
 *
 * Restricted material findings (restricted = true / title null) render as a
 * "Restricted — pending chair review" placeholder; the backend has already
 * stripped the detail per requester.
 */
@Component({
  selector: 'app-ac-analytics-sections',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatCardModule, MatIconModule, MatTableModule, MatTooltipModule],
  templateUrl: './analytics-sections.component.html',
  styleUrl: './analytics-sections.component.scss',
})
export class AcAnalyticsSectionsComponent {
  readonly totalPlans = input.required<number>();
  readonly planItemsTotal = input.required<number>();
  readonly planItemsCompleted = input.required<number>();
  readonly planCompletionPercent = input.required<number>();
  readonly openExceptionTotal = input.required<number>();
  readonly averageClosureDays = input.required<number | null>();
  readonly exceptionsBySeverity = input.required<AcSeverityCount[]>();
  readonly materialFindings = input.required<AcMaterialFinding[]>();
  readonly sanctionsTotalCases = input.required<number>();
  readonly sanctionsGridAdherencePercent = input.required<number>();
  readonly sanctionsAppealRatePercent = input.required<number>();
  readonly sanctionsByBusinessUnit =
    input.required<AcSanctionsConsistencyRow[]>();
  readonly recurrenceClusters = input.required<AcRecurrenceCluster[]>();

  readonly humanise = humanise;
  readonly percent = percent;
  readonly days = days;

  readonly findingColumns = ['title', 'severity', 'status', 'raisedAt', 'targetDate'];
  readonly sanctionsColumns = [
    'businessUnit',
    'caseCount',
    'gridAdherence',
    'deviationCount',
    'appealRate',
  ];
  readonly recurrenceColumns = ['category', 'count', 'window', 'lastOccurred'];
}