import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
  CdkDropList,
  CdkDrag,
  CdkDragHandle,
  type CdkDragDrop,
  moveItemInArray,
} from '@angular/cdk/drag-drop';
import { forkJoin, map, of } from 'rxjs';

import { AnnualPlansService } from '../../../core/services/annual-plans.service';
import { UniverseService } from '../../../core/services/universe.service';
import { UsersService } from '../../../core/services/users.service';
import { TemplatesService } from '../../../core/services/templates.service';
import { AuditsService } from '../../../core/services/audits.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { EntityLookupService } from '../../../core/services/entity-lookup.service';
import { ReferenceDataService } from '../../../core/services/reference-data.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  AddPlanItemRequest,
  CreateAuditRequest,
  EntityListItem,
  Plan,
  PlanDecisionRequest,
  PlanExecution,
  PlanItem,
  PlanItemEntityLink,
  PlanItemProgress,
  ReferenceDataItem,
  SubmitRevisionRequest,
  TemplateListItem,
  UserDto,
} from '../../../core/models';
import {
  CreateAuditDialogComponent,
  CreateAuditDialogData,
  CreateAuditPlanItemContext,
} from '../../audits/dialogs/create-audit-dialog.component';
import {
  PlanItemDialogComponent,
  PlanItemDialogData,
} from '../dialogs/plan-item-dialog.component';
import {
  SubmitRevisionDialogComponent,
  SubmitRevisionDialogData,
} from '../dialogs/submit-revision-dialog.component';
import { PlanDecisionDialogComponent } from '../dialogs/plan-decision-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { GanttChartComponent } from '../../../shared/charts';
import { GanttItem } from '../../../shared/charts/chart-types';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for a single annual plan (walkthrough + "About this page" panel). */
const PLAN_DETAIL_GUIDE: PageGuide = {
  id: 'planning-plan-detail',
  titleKey: 'planning.detail.guide.pageTitle',
  purposeKey: 'planning.detail.guide.purpose',
  descriptionKey: 'planning.detail.guide.description',
  actionKeys: [
    'planning.detail.guide.action.items',
    'planning.detail.guide.action.workflow',
    'planning.detail.guide.action.launch',
    'planning.detail.guide.action.track',
  ],
  sections: [
    { selector: '.detail__actions', titleKey: 'planning.detail.guide.section.actions.title', bodyKey: 'planning.detail.guide.section.actions.body' },
    { selector: '.detail__status-row', titleKey: 'planning.detail.guide.section.status.title', bodyKey: 'planning.detail.guide.section.status.body' },
    { selector: '.detail__exec-card', titleKey: 'planning.detail.guide.section.execution.title', bodyKey: 'planning.detail.guide.section.execution.body' },
    { selector: '[data-guide="timeline"]', titleKey: 'planning.detail.guide.section.timeline.title', bodyKey: 'planning.detail.guide.section.timeline.body' },
    { selector: '.detail__items-card', titleKey: 'planning.detail.guide.section.items.title', bodyKey: 'planning.detail.guide.section.items.body' },
  ],
  workflowKeys: [
    'planning.detail.guide.flow.draft',
    'planning.detail.guide.flow.submit',
    'planning.detail.guide.flow.decide',
    'planning.detail.guide.flow.launch',
    'planning.detail.guide.flow.close',
  ],
  dependsOnKeys: [
    'planning.detail.guide.dep.universe',
    'planning.detail.guide.dep.users',
    'planning.detail.guide.dep.refdata',
  ],
  usedByKeys: [
    'planning.detail.guide.use.audits',
    'planning.detail.guide.use.coverage',
    'planning.detail.guide.use.reports',
  ],
  businessRuleKeys: [
    'planning.detail.guide.rule.editable',
    'planning.detail.guide.rule.approval',
    'planning.detail.guide.rule.launch',
    'planning.detail.guide.rule.revision',
  ],
  tipKeys: [
    'planning.detail.guide.tip.reorder',
    'planning.detail.guide.tip.effort',
    'planning.detail.guide.tip.behind',
  ],
  permissionKeys: [
    'planning.detail.guide.perm.manager',
    'planning.detail.guide.perm.chair',
  ],
  faq: [
    { questionKey: 'planning.detail.guide.faq.launch.q', answerKey: 'planning.detail.guide.faq.launch.a' },
    { questionKey: 'planning.detail.guide.faq.revision.q', answerKey: 'planning.detail.guide.faq.revision.a' },
  ],
};

@Component({
  selector: 'app-plan-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    MatProgressBarModule,
    MatTooltipModule,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    GanttChartComponent,
    TranslatePipe,
  ],
  templateUrl: './plan-detail.component.html',
  styleUrl: './plan-detail.component.scss',
})
export class PlanDetailComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(AnnualPlansService);
  private readonly universe = inject(UniverseService);
  private readonly users = inject(UsersService);
  private readonly templates = inject(TemplatesService);
  private readonly audits = inject(AuditsService);
  /** Resolves assigned-lead user ids to display names in the item table. */
  readonly userLookup = inject(UserLookupService);
  /** Resolves entity ids to names for the timeline row labels. */
  private readonly entityLookup = inject(EntityLookupService);
  private readonly refData = inject(ReferenceDataService);
  /** Resolves the stored audit-type code to its human label (lazy-loaded, incl. inactive). */
  readonly refLookup = inject(ReferenceDataLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslationService);

  /** A drag handle column is shown only while the plan's items are reorderable. */
  readonly itemColumns = computed(() =>
    this.isEditable()
      ? ['drag', 'auditType', 'entities', 'planned', 'effort', 'lead', 'status', 'actions']
      : ['auditType', 'entities', 'planned', 'effort', 'lead', 'status', 'actions'],
  );

  /** Human name for an entity link, resolved through the directory (falls back to the id while loading). */
  entityName(link: PlanItemEntityLink): string {
    return this.entityLookup.name(link.entityId);
  }

  readonly guide = PLAN_DETAIL_GUIDE;

  readonly state = signal<ViewState>('loading');
  readonly plan = signal<Plan | null>(null);
  readonly execution = signal<PlanExecution | null>(null);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManagePlan),
  );
  readonly canDecide = computed(() =>
    this.auth.hasPermission(Permissions.ACChair),
  );

  /** Items can be added/removed only while the plan is editable. */
  readonly isEditable = computed(() => {
    const s = this.plan()?.status;
    return s === 'draft' || s === 'revisions_requested';
  });

  readonly canSubmit = computed(() => {
    const s = this.plan()?.status;
    return (
      this.canManage() && (s === 'draft' || s === 'revisions_requested')
    );
  });

  readonly canSubmitRevision = computed(() => {
    const s = this.plan()?.status;
    return this.canManage() && (s === 'approved' || s === 'revision_submitted');
  });

  /** AC Chair can decide only while the plan is awaiting a decision. */
  readonly canDecideNow = computed(() => {
    const s = this.plan()?.status;
    return (
      this.canDecide() && (s === 'submitted' || s === 'revision_submitted')
    );
  });

  readonly canClose = computed(() => {
    const s = this.plan()?.status;
    return this.canManage() && s === 'approved';
  });

  /** The user is allowed to create audits at all (drives whether the Launch affordance is shown). */
  readonly canCreateAudit = computed(() =>
    this.auth.hasPermission(Permissions.CreateAudit),
  );

  /**
   * Audits can actually be launched now: the backend says the plan is in a linkable state
   * (Approved, or a deployment that allows pre-approval launch) AND the user may create audits.
   * When false but {@link canCreateAudit} is true, the button is shown disabled with a hint so the
   * plan → audit workflow is always discoverable rather than silently absent.
   */
  readonly canLaunchAudits = computed(
    () => !!this.plan()?.canLaunchAudits && this.canCreateAudit(),
  );

  /** Plan items laid out for the timeline (Gantt), ordered by their manual order index. */
  readonly ganttItems = computed<GanttItem[]>(() => {
    const p = this.plan();
    if (!p) {
      return [];
    }
    return [...p.items]
      .sort((a, b) => a.orderIndex - b.orderIndex)
      .map((it) => ({
        label: it.entityLinks.map((l) => this.entityLookup.name(l.entityId)).join(', '),
        start: it.plannedStartDate,
        end: it.plannedEndDate,
        tone: it.status,
        detail: `${it.auditType} · ${it.plannedStartDate} → ${it.plannedEndDate}`,
      }));
  });

  /** Real per-entity checklist progress from the execution roll-up, for one entity of a plan item. */
  progressFor(item: PlanItem, entityId: string): PlanItemProgress | undefined {
    return this.execution()?.itemProgress?.find(
      (p) => p.planItemId === item.id && p.entityId === entityId,
    );
  }

  readonly statusCounts = computed(() => {
    const exec = this.execution();
    if (!exec) {
      return [];
    }
    return Object.entries(exec.countsByStatus).map(([status, count]) => ({
      status,
      count,
    }));
  });

  private entitiesCache: EntityListItem[] = [];

  constructor() {
    // Warm the entity directory so the timeline row labels resolve to names, not ids.
    this.entityLookup.ensureLoaded();
    // The id input resolves before the constructor body runs in zoneless mode
    // only after binding; fetch lazily via effect-free guard.
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getById(this.id()).subscribe({
      next: (plan) => {
        this.plan.set(plan);
        this.state.set('ready');
        this.loadExecution();
      },
      error: () => this.state.set('error'),
    });
  }

  private loadExecution(): void {
    this.service.execution(this.id()).subscribe({
      next: (exec) => this.execution.set(exec),
      error: () => {
        // Execution is supplementary; leave the panel empty on failure.
      },
    });
  }

  private refresh(): void {
    this.service.getById(this.id()).subscribe({
      next: (plan) => {
        this.plan.set(plan);
        this.loadExecution();
      },
    });
  }

  addItem(): void {
    const openDialog = (entities: EntityListItem[]): void => {
      const p = this.plan();
      const data: PlanItemDialogData = {
        entities,
        planPeriodStart: p?.periodStart ?? '',
        planPeriodEnd: p?.periodEnd ?? '',
      };
      this.dialog
        .open(PlanItemDialogComponent, { data, width: '600px' })
        .afterClosed()
        .subscribe((result?: AddPlanItemRequest) => {
          if (!result) {
            return;
          }
          this.service.addItem(this.id(), result).subscribe({
            next: () => {
              this.notify.success(this.i18n.translate('planning.toast.itemAdded'));
              this.refresh();
            },
          });
        });
    };

    if (this.entitiesCache.length) {
      openDialog(this.entitiesCache);
      return;
    }
    this.universe.list({ pageSize: 0 }).subscribe({
      next: (page) => {
        this.entitiesCache = page.items;
        openDialog(page.items);
      },
      error: () => openDialog([]),
    });
  }

  /**
   * Launch an audit for one entity of an approved plan item: fetch the entities / users / published templates,
   * open the create-audit dialog prefilled from the item + chosen entity, and on create link the audit back to
   * that entity link (the backend sets its linkedAuditId), then jump to the new audit. A plan item can cover
   * several entities — each is launched, tracked and completed independently.
   */
  launchAudit(item: PlanItem, link: PlanItemEntityLink): void {
    const open = (
      entities: EntityListItem[],
      users: UserDto[],
      templates: TemplateListItem[],
      auditTypes: ReferenceDataItem[],
    ): void => {
      const typeLabel =
        auditTypes.find((t) => t.code === item.auditType)?.label ?? item.auditType;
      const entity = entities.find((e) => e.id === link.entityId)?.name;
      const entityName = entity ?? typeLabel;
      const planItem: CreateAuditPlanItemContext = {
        planItemId: item.id,
        auditType: item.auditType,
        auditTypeLabel: typeLabel,
        leadUserId: item.assignedLeadUserId,
        entityId: link.entityId,
        entityName,
        suggestedName: entity ? `${entity} — ${typeLabel}` : typeLabel,
        plannedStartDate: item.plannedStartDate,
        plannedEndDate: item.plannedEndDate,
      };
      const data: CreateAuditDialogData = { users, templates, planItem };
      this.dialog
        .open(CreateAuditDialogComponent, { data, width: '640px' })
        .afterClosed()
        .subscribe((result?: CreateAuditRequest) => {
          if (!result) {
            return;
          }
          this.audits.create(result).subscribe({
            next: (created) => {
              this.notify.success(
                this.i18n.translate('planning.toast.auditLaunched', {
                  name: created.name,
                }),
              );
              this.refresh(); // this entity link now carries linkedAuditId → shows "Open audit"
              void this.router.navigate(['/audits', created.id]);
            },
          });
        });
    };

    forkJoin({
      entities: this.entitiesCache.length
        ? of(this.entitiesCache)
        : this.universe.list({ pageSize: 0 }).pipe(map((p) => p.items)),
      users: this.users.list({ status: 'active', pageSize: 0 }).pipe(map((p) => p.items)),
      templates: this.templates
        .list({ status: 'published', pageSize: 0 })
        .pipe(map((p) => p.items)),
      auditTypes: this.refData.list('audit_type'),
    }).subscribe({
      next: ({ entities, users, templates, auditTypes }) => {
        this.entitiesCache = entities;
        open(entities, users, templates, auditTypes);
      },
      error: () => open(this.entitiesCache, [], [], []),
    });
  }

  /** Open the audit already launched for this entity link. */
  openLinkedAudit(link: PlanItemEntityLink): void {
    if (link.linkedAuditId) {
      void this.router.navigate(['/audits', link.linkedAuditId]);
    }
  }

  /** Reorder plan items by dragging a row. Only permitted while the plan is editable. */
  dropItem(event: CdkDragDrop<PlanItem[]>): void {
    const p = this.plan();
    if (!p || !this.isEditable() || event.previousIndex === event.currentIndex) {
      return;
    }
    const items = [...p.items];
    moveItemInArray(items, event.previousIndex, event.currentIndex);
    // Optimistically reflect the new order (and re-stamp orderIndex) so the row doesn't snap back.
    this.plan.set({
      ...p,
      items: items.map((it, index) => ({ ...it, orderIndex: index })),
    });
    this.service
      .reorderItems(this.id(), { orderedItemIds: items.map((i) => i.id) })
      .subscribe({ next: () => this.refresh(), error: () => this.refresh() });
  }

  removeItem(item: PlanItem): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('planning.remove.title'),
      message: this.i18n.translate('planning.remove.message', {
        type: item.auditType,
      }),
      confirmLabel: this.i18n.translate('planning.remove.confirm'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.service.removeItem(this.id(), item.id).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('planning.toast.itemRemoved'));
            this.refresh();
          },
        });
      });
  }

  submit(): void {
    this.service.submit(this.id()).subscribe({
      next: (plan) => {
        this.plan.set(plan);
        this.notify.success(this.i18n.translate('planning.toast.submitted'));
        this.loadExecution();
      },
    });
  }

  submitRevision(): void {
    const data: SubmitRevisionDialogData = {
      items: this.plan()?.items ?? [],
      canApplyMinorRevision: this.plan()?.canApplyMinorRevision ?? false,
    };
    this.dialog
      .open(SubmitRevisionDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: SubmitRevisionRequest) => {
        if (!result) {
          return;
        }
        this.service.submitRevision(this.id(), result).subscribe({
          next: (plan) => {
            this.plan.set(plan);
            this.notify.success(this.i18n.translate('planning.toast.revisionSubmitted'));
            this.loadExecution();
          },
        });
      });
  }

  decide(): void {
    this.dialog
      .open(PlanDecisionDialogComponent, { width: '520px' })
      .afterClosed()
      .subscribe((result?: PlanDecisionRequest) => {
        if (!result) {
          return;
        }
        this.service.decision(this.id(), result).subscribe({
          next: (plan) => {
            this.plan.set(plan);
            this.notify.success(
              this.i18n.translate('planning.toast.decisionRecorded', {
                decision: result.decision,
              }),
            );
          },
        });
      });
  }

  close(): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('planning.close.title'),
      message: this.i18n.translate('planning.close.message'),
      confirmLabel: this.i18n.translate('planning.close.confirm'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.service.close(this.id()).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('planning.toast.closed'));
            this.refresh();
          },
        });
      });
  }
}
