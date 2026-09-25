import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatMenuModule } from '@angular/material/menu';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Observable, concat, last } from 'rxjs';

import { OrgUnitService } from '../../../core/services/org-unit.service';
import { OrgUnitLookupService } from '../../../core/services/org-unit-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { OrgUnit } from '../../../core/models';
import {
  OrgUnitDialogComponent,
  OrgUnitDialogData,
  OrgUnitDialogResult,
} from '../dialogs/org-unit-dialog.component';
import { SelectOption } from '../../../shared/components/searchable-select/searchable-select.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** A unit plus its computed tree depth, for indented rendering. */
interface OrgUnitRow extends OrgUnit {
  depth: number;
}

/** Contextual page guide for org-unit administration (drives the walkthrough + the About panel). */
const ORG_UNITS_GUIDE: PageGuide = {
  id: 'org-units',
  titleKey: 'orgUnit.list.title',
  purposeKey: 'orgUnit.list.guide.purpose',
  descriptionKey: 'orgUnit.list.guide.description',
  actionKeys: ['orgUnit.list.guide.action.create', 'orgUnit.list.guide.action.reparent', 'orgUnit.list.guide.action.archive'],
  sections: [
    { selector: '.orgunits__table-card', titleKey: 'orgUnit.list.guide.section.tree.title', bodyKey: 'orgUnit.list.guide.section.tree.body' },
  ],
  usedByKeys: ['orgUnit.list.guide.use.entities', 'orgUnit.list.guide.use.users', 'orgUnit.list.guide.use.scorecards'],
  businessRuleKeys: ['orgUnit.list.guide.rule.noCycles', 'orgUnit.list.guide.rule.uniqueCode', 'orgUnit.list.guide.rule.archiveKeepsHistory'],
  tipKeys: ['orgUnit.list.guide.tip.indent'],
  permissionKeys: ['orgUnit.list.guide.perm.manage'],
};

/**
 * Org-unit (department / business-unit) administration. Manages the hierarchy
 * that audit-universe entities and users are assigned to and that the
 * department scorecards aggregate over.
 */
@Component({
  selector: 'app-org-units',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatCheckboxModule,
    MatButtonModule,
    IconComponent,
    MatMenuModule,
    RouterLink,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './org-units.component.html',
  styleUrl: './org-units.component.scss',
})
export class OrgUnitsComponent {
  private readonly service = inject(OrgUnitService);
  private readonly lookup = inject(OrgUnitLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = ['name', 'code', 'status', 'actions'];

  readonly filters = this.fb.nonNullable.group({
    includeArchived: false,
  });

  readonly state = signal<ViewState>('loading');
  private readonly units = signal<OrgUnit[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageUniverse),
  );

  /** Units flattened in pre-order with a depth, so the tree renders indented. */
  readonly rows = computed<OrgUnitRow[]>(() => this.toTree(this.units()));

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.units().length === 0,
  );

  readonly guide = ORG_UNITS_GUIDE;

  constructor() {
    this.fetch();
    this.filters.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.list(this.filters.getRawValue().includeArchived).subscribe({
      next: (items) => {
        this.units.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  create(): void {
    const data: OrgUnitDialogData = { parentOptions: this.parentOptions(null) };
    this.dialog
      .open(OrgUnitDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: OrgUnitDialogResult) => {
        if (!result || result.mode !== 'create') {
          return;
        }
        this.service.create(result.body).subscribe({
          next: (created) => {
            this.notify.success(
              this.i18n.translate('orgUnit.notify.created', { name: created.name }),
            );
            this.afterMutation();
          },
          error: (err: unknown) => this.onMutationError(err),
        });
      });
  }

  edit(unit: OrgUnit): void {
    const data: OrgUnitDialogData = {
      unit,
      parentOptions: this.parentOptions(unit.id),
    };
    this.dialog
      .open(OrgUnitDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((result?: OrgUnitDialogResult) => {
        if (!result || result.mode !== 'edit') {
          return;
        }
        const ops: Observable<OrgUnit>[] = [];
        if (result.name !== result.original.name) {
          ops.push(this.service.rename(result.id, { name: result.name }));
        }
        if (result.parentOrgUnitId !== result.original.parentOrgUnitId) {
          ops.push(
            this.service.reparent(result.id, {
              parentOrgUnitId: result.parentOrgUnitId,
            }),
          );
        }
        if (result.headUserId !== result.original.headUserId) {
          ops.push(this.service.setHead(result.id, result.headUserId));
        }
        if (ops.length === 0) {
          return;
        }
        // Sequential: the two endpoints mutate the same aggregate.
        concat(...ops)
          .pipe(last())
          .subscribe({
            next: (updated) => {
              this.notify.success(
                this.i18n.translate('orgUnit.notify.updated', { name: updated.name }),
              );
              this.afterMutation();
            },
            error: (err: unknown) => this.onMutationError(err),
          });
      });
  }

  archive(unit: OrgUnit): void {
    this.service.archive(unit.id).subscribe({
      next: () => {
        this.notify.success(
          this.i18n.translate('orgUnit.notify.archived', { name: unit.name }),
        );
        this.afterMutation();
      },
      error: (err: unknown) => this.onMutationError(err),
    });
  }

  restore(unit: OrgUnit): void {
    this.service.restore(unit.id).subscribe({
      next: () => {
        this.notify.success(
          this.i18n.translate('orgUnit.notify.restored', { name: unit.name }),
        );
        this.afterMutation();
      },
      error: (err: unknown) => this.onMutationError(err),
    });
  }

  indent(depth: number): string {
    return `${depth * 1.25}rem`;
  }

  private afterMutation(): void {
    this.lookup.reload();
    this.fetch();
  }

  private onMutationError(err: unknown): void {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 409 && this.errorCode(err) === 'org_unit.cycle_detected') {
        this.notify.error(this.i18n.translate('orgUnit.error.cycle'));
        return;
      }
      if (err.status === 409 && this.errorCode(err) === 'org_unit.code_taken') {
        this.notify.error(this.i18n.translate('orgUnit.error.codeTaken'));
        return;
      }
    }
    // Other errors already surfaced by the global interceptor.
  }

  private errorCode(err: HttpErrorResponse): string | undefined {
    return (err.error as { error_code?: string } | null)?.error_code;
  }

  /** Non-archived units as parent options (full-path labels), excluding `excludeId`. */
  private parentOptions(excludeId: string | null): SelectOption[] {
    const active = this.units().filter((u) => !u.isArchived && u.id !== excludeId);
    const byId = new Map(active.map((u) => [u.id, u]));
    return active
      .map((u) => ({ value: u.id, label: this.pathFor(u.id, byId) }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }

  /** Builds "Root / … / Unit"; cycle-guarded. */
  private pathFor(id: string, byId: Map<string, OrgUnit>): string {
    const parts: string[] = [];
    const seen = new Set<string>();
    let current: OrgUnit | undefined = byId.get(id);
    while (current && !seen.has(current.id)) {
      seen.add(current.id);
      parts.unshift(current.name);
      current = current.parentOrgUnitId ? byId.get(current.parentOrgUnitId) : undefined;
    }
    return parts.join(' / ');
  }

  /** Flattens the adjacency list into a pre-ordered, depth-tagged list. */
  private toTree(units: OrgUnit[]): OrgUnitRow[] {
    const byId = new Map(units.map((u) => [u.id, u]));
    const childrenByParent = new Map<string | null, OrgUnit[]>();
    for (const u of units) {
      // A unit whose parent is missing (e.g. archived-out) is treated as a root.
      const parent = u.parentOrgUnitId && byId.has(u.parentOrgUnitId) ? u.parentOrgUnitId : null;
      const bucket = childrenByParent.get(parent) ?? [];
      bucket.push(u);
      childrenByParent.set(parent, bucket);
    }
    for (const bucket of childrenByParent.values()) {
      bucket.sort((a, b) => a.name.localeCompare(b.name));
    }

    const rows: OrgUnitRow[] = [];
    const seen = new Set<string>();
    const walk = (parent: string | null, depth: number): void => {
      for (const u of childrenByParent.get(parent) ?? []) {
        if (seen.has(u.id)) {
          continue;
        }
        seen.add(u.id);
        rows.push({ ...u, depth });
        walk(u.id, depth + 1);
      }
    };
    walk(null, 0);
    return rows;
  }
}
