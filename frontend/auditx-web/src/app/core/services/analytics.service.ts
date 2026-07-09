import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  ApiResponse,
  BudgetVsActualRow,
  CoverageMatrix,
  CursorPage,
  DashboardDetail,
  DashboardListItem,
  DeleteDashboardWidgetRequest,
  ExceptionPortfolio,
  FunctionPerformance,
  MaterialFinding,
  OrgUnitScorecard,
  PerformanceScorecard,
  PlanStatusKpi,
  RecurrenceCluster,
  RecurrenceClusterDetail,
  SanctionsConsistency,
  SaveDashboardWidgetRequest,
  UtilisationRow,
} from '../models';

/**
 * Typed client for the M9 Advanced Analytics & Dashboards endpoints.
 *
 * Dashboards are listed/viewed under `/dashboards`; each KPI projection lives
 * under `/analytics`. Every widget mutation echoes the dashboard rowversion
 * (`version`) for optimistic concurrency and returns the freshly-recomputed
 * {@link DashboardDetail}. The widget DELETE carries its version in the request
 * body (the backend reads `[FromBody]`), so it talks to {@link HttpClient}
 * directly — {@link ApiService.deleteVoid} cannot send a body.
 *
 * CRITICAL: no analytics projection returns a sanctions subject identity — the
 * API aggregates sanctions by business unit / category only.
 */
@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /* ---- Dashboards ---- */

  /** Dashboards the caller may see (already filtered server-side). */
  listDashboards(): Observable<DashboardListItem[]> {
    return this.api.get<DashboardListItem[]>('/dashboards');
  }

  /** A fully-assembled dashboard with each visible widget's computed data. */
  getDashboard(idOrSlug: string): Observable<DashboardDetail> {
    return this.api.get<DashboardDetail>(`/dashboards/${idOrSlug}`);
  }

  /** Adds a widget (ConfigureDashboards). Returns the refreshed dashboard. */
  addWidget(
    dashboardId: string,
    body: SaveDashboardWidgetRequest,
  ): Observable<DashboardDetail> {
    return this.api.post<DashboardDetail>(
      `/dashboards/${dashboardId}/widgets`,
      body,
    );
  }

  /** Edits a widget (ConfigureDashboards). Returns the refreshed dashboard. */
  updateWidget(
    dashboardId: string,
    widgetId: string,
    body: SaveDashboardWidgetRequest,
  ): Observable<DashboardDetail> {
    return this.api.patch<DashboardDetail>(
      `/dashboards/${dashboardId}/widgets/${widgetId}`,
      body,
    );
  }

  /**
   * Removes a widget (ConfigureDashboards). The version travels in the request
   * body, so this bypasses {@link ApiService} and reads the unwrapped envelope.
   */
  deleteWidget(
    dashboardId: string,
    widgetId: string,
    body: DeleteDashboardWidgetRequest,
  ): Observable<DashboardDetail> {
    return this.http
      .delete<ApiResponse<DashboardDetail>>(
        `${this.baseUrl}/dashboards/${dashboardId}/widgets/${widgetId}`,
        { body, withCredentials: true },
      )
      .pipe(map((r) => r.data));
  }

  /* ---- Analytics KPIs (ViewAnalytics) ---- */

  functionPerformance(): Observable<FunctionPerformance> {
    return this.api.get<FunctionPerformance>('/analytics/function-performance');
  }

  exceptionPortfolio(): Observable<ExceptionPortfolio> {
    return this.api.get<ExceptionPortfolio>('/analytics/exception-portfolio');
  }

  /** Aggregated by business unit; never carries a sanctions subject identity. */
  sanctionsConsistency(): Observable<SanctionsConsistency> {
    return this.api.get<SanctionsConsistency>('/analytics/sanctions-consistency');
  }

  materialFindings(): Observable<MaterialFinding[]> {
    return this.api.get<MaterialFinding[]>('/analytics/material-findings');
  }

  planStatus(): Observable<PlanStatusKpi> {
    return this.api.get<PlanStatusKpi>('/analytics/plan-status');
  }

  /** Coverage matrix (reuses M3). Optional window in months (defaults to 12). */
  coverage(windowMonths?: number): Observable<CoverageMatrix> {
    return this.api.get<CoverageMatrix>('/analytics/coverage', {
      windowMonths,
    });
  }

  /** Per-audit-lead performance scorecards (PerformanceAnalyticsView). */
  performanceScorecards(): Observable<PerformanceScorecard[]> {
    return this.api.get<PerformanceScorecard[]>(
      '/analytics/performance-scorecards',
    );
  }

  /**
   * Department / business-unit scorecards (ViewAnalytics). Each row aggregates
   * the org unit plus all of its descendants; ordered pre-order with a `depth`
   * for indented tree rendering.
   */
  orgUnitScorecards(): Observable<OrgUnitScorecard[]> {
    return this.api.get<OrgUnitScorecard[]>('/analytics/org-units');
  }

  /** Budget-vs-actual per audit — budgeted hours vs logged time (P0-B, ViewAnalytics). */
  budgetVsActual(): Observable<BudgetVsActualRow[]> {
    return this.api.get<BudgetVsActualRow[]>('/analytics/budget-vs-actual');
  }

  /** Utilisation per auditor — logged hours split by activity category (P0-B, ViewAnalytics). */
  utilisation(): Observable<UtilisationRow[]> {
    return this.api.get<UtilisationRow[]>('/analytics/utilisation');
  }

  /* ---- Recurrence clusters (ViewAnalytics) ---- */

  /** Cursor-paged list of detected recurrence clusters. */
  recurrenceClusters(
    cursor?: string | null,
    limit?: number,
  ): Observable<CursorPage<RecurrenceCluster>> {
    return this.api.get<CursorPage<RecurrenceCluster>>(
      '/analytics/recurrence-clusters',
      { cursor, limit },
    );
  }

  /** A recurrence cluster with its member exceptions (drilldown). */
  recurrenceClusterDetail(id: string): Observable<RecurrenceClusterDetail> {
    return this.api.get<RecurrenceClusterDetail>(
      `/analytics/recurrence-clusters/${id}`,
    );
  }
}
