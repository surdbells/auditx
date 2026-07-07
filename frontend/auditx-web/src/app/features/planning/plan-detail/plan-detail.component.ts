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
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  CdkDropList,
  CdkDrag,
  CdkDragHandle,
  type CdkDragDrop,
  moveItemInArray,
} from '@angular/cdk/drag-drop';

import { AnnualPlansService } from '../../../core/services/annual-plans.service';
import { UniverseService } from '../../../core/services/universe.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  AddPlanItemRequest,
  EntityListItem,
  Plan,
  PlanDecisionRequest,
  PlanExecution,
  PlanItem,
  SubmitRevisionRequest,
} from '../../../core/models';
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

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-plan-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './plan-detail.component.html',
  styleUrl: './plan-detail.component.scss',
})
export class PlanDetailComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(AnnualPlansService);
  private readonly universe = inject(UniverseService);
  /** Resolves assigned-lead user ids to display names in the item table. */
  readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  /** A drag handle column is shown only while the plan's items are reorderable. */
  readonly itemColumns = computed(() =>
    this.isEditable()
      ? ['drag', 'auditType', 'planned', 'effort', 'lead', 'status', 'actions']
      : ['auditType', 'planned', 'effort', 'lead', 'status', 'actions'],
  );

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
      const data: PlanItemDialogData = { entities };
      this.dialog
        .open(PlanItemDialogComponent, { data, width: '600px' })
        .afterClosed()
        .subscribe((result?: AddPlanItemRequest) => {
          if (!result) {
            return;
          }
          this.service.addItem(this.id(), result).subscribe({
            next: () => {
              this.notify.success('Plan item added.');
              this.refresh();
            },
          });
        });
    };

    if (this.entitiesCache.length) {
      openDialog(this.entitiesCache);
      return;
    }
    this.universe.list({ limit: 100 }).subscribe({
      next: (page) => {
        this.entitiesCache = page.items;
        openDialog(page.items);
      },
      error: () => openDialog([]),
    });
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
      title: 'Remove plan item',
      message: `Remove the "${item.auditType}" item from this plan?`,
      confirmLabel: 'Remove',
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
            this.notify.success('Plan item removed.');
            this.refresh();
          },
        });
      });
  }

  submit(): void {
    this.service.submit(this.id()).subscribe({
      next: (plan) => {
        this.plan.set(plan);
        this.notify.success('Plan submitted for approval.');
        this.loadExecution();
      },
    });
  }

  submitRevision(): void {
    const data: SubmitRevisionDialogData = {
      items: this.plan()?.items ?? [],
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
            this.notify.success('Revision submitted.');
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
            this.notify.success(`Decision recorded: ${result.decision}.`);
          },
        });
      });
  }

  close(): void {
    const data: ConfirmDialogData = {
      title: 'Close plan',
      message: 'Close this plan? It will become read-only.',
      confirmLabel: 'Close plan',
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
            this.notify.success('Plan closed.');
            this.refresh();
          },
        });
      });
  }
}
