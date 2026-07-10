import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { SanctionsService } from '../../../../core/services/sanctions.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { ReferenceDataLookupService } from '../../../../core/services/reference-data-lookup.service';
import {
  GridDefinition,
  SanctionsGridVersion,
} from '../../../../core/models';
import { humanise } from '../humanise';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import {
  SanctionsReasonDialogComponent,
  SanctionsReasonDialogData,
  SanctionsReasonResult,
} from '../dialogs/sanctions-reason-dialog.component';

type ViewState = 'loading' | 'ready' | 'error' | 'empty';

/** Contextual page guide for the sanctions grid admin (walkthrough + About panel). */
const SANCTIONS_GRID_GUIDE: PageGuide = {
  id: 'sanctions-grid-admin',
  titleKey: 'sanctions.grid.title',
  purposeKey: 'sanctions.grid.guide.purpose',
  descriptionKey: 'sanctions.grid.guide.description',
  actionKeys: [
    'sanctions.grid.guide.action.add',
    'sanctions.grid.guide.action.range',
    'sanctions.grid.guide.action.save',
    'sanctions.grid.guide.action.activate',
  ],
  sections: [
    { selector: '.grid__table', titleKey: 'sanctions.grid.guide.section.matrix.title', bodyKey: 'sanctions.grid.guide.section.matrix.body' },
    { selector: '.grid__add', titleKey: 'sanctions.grid.guide.section.add.title', bodyKey: 'sanctions.grid.guide.section.add.body' },
    { selector: '.grid__actions', titleKey: 'sanctions.grid.guide.section.actions.title', bodyKey: 'sanctions.grid.guide.section.actions.body' },
  ],
  workflowKeys: [
    'sanctions.grid.guide.flow.finding',
    'sanctions.grid.guide.flow.classify',
    'sanctions.grid.guide.flow.grid',
    'sanctions.grid.guide.flow.recommend',
    'sanctions.grid.guide.flow.apply',
  ],
  dependsOnKeys: [
    'sanctions.grid.guide.dep.categories',
    'sanctions.grid.guide.dep.severity',
    'sanctions.grid.guide.dep.approval',
    'sanctions.grid.guide.dep.findings',
  ],
  usedByKeys: [
    'sanctions.grid.guide.use.recommend',
    'sanctions.grid.guide.use.findings',
    'sanctions.grid.guide.use.actions',
    'sanctions.grid.guide.use.reports',
  ],
  businessRuleKeys: [
    'sanctions.grid.guide.rule.version',
    'sanctions.grid.guide.rule.makerchecker',
    'sanctions.grid.guide.rule.key',
    'sanctions.grid.guide.rule.reason',
  ],
  tipKeys: [
    'sanctions.grid.guide.tip.recurrence',
    'sanctions.grid.guide.tip.range',
    'sanctions.grid.guide.tip.activate',
  ],
  permissionKeys: [
    'sanctions.grid.guide.perm.view',
    'sanctions.grid.guide.perm.manage',
    'sanctions.grid.guide.perm.approve',
  ],
  faq: [
    { questionKey: 'sanctions.grid.guide.faq.pending.q', answerKey: 'sanctions.grid.guide.faq.pending.a' },
    { questionKey: 'sanctions.grid.guide.faq.active.q', answerKey: 'sanctions.grid.guide.faq.active.a' },
  ],
};

/** One editable row of the grid matrix. */
interface GridRow {
  key: string;
  category: string;
  severity: string;
  isRecurrence: boolean;
  recommendedRange: string;
}

@Component({
  selector: 'app-sanctions-grid-admin',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './sanctions-grid-admin.component.html',
  styleUrl: './sanctions-grid-admin.component.scss',
})
export class SanctionsGridAdminComponent {
  private readonly service = inject(SanctionsService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);
  private readonly refLookup = inject(ReferenceDataLookupService);

  /** Managed sanction-category options (reference-data category "sanction_category"). */
  readonly categories = this.refLookup.options('sanction_category');

  /** Resolves a stored category code to its human label (falls back to the raw code). */
  categoryLabel(code: string): string {
    return this.refLookup.label('sanction_category', code);
  }

  readonly state = signal<ViewState>('loading');
  readonly grid = signal<SanctionsGridVersion | null>(null);
  readonly rows = signal<GridRow[]>([]);
  /** Banner text when a save/activate was routed to a second approver. */
  readonly pendingBanner = signal<string | null>(null);
  readonly saving = signal(false);

  /** Severity keys — case matters, they form part of the grid cell key. */
  readonly severities = ['low', 'medium', 'high', 'critical'];

  /** New blank cell descriptors. */
  newCategory = '';
  newSeverity = '';
  newRecurrence = false;

  readonly humanise = humanise;

  readonly guide = SANCTIONS_GRID_GUIDE;

  readonly canActivate = computed(() => {
    const g = this.grid();
    return g !== null && !g.isActive;
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getActiveGrid().subscribe({
      next: (g) => {
        this.grid.set(g);
        if (g) {
          this.rows.set(this.parseRows(g.gridDefinitionJson));
          this.state.set('ready');
        } else {
          this.rows.set([]);
          this.state.set('empty');
        }
      },
      error: () => this.state.set('error'),
    });
  }

  private parseRows(json: string): GridRow[] {
    let parsed: GridDefinition;
    try {
      parsed = JSON.parse(json) as GridDefinition;
    } catch {
      return [];
    }
    const cells = parsed.cells ?? {};
    return Object.entries(cells).map(([key, cell]) => {
      const [category, severity, recurrence] = key.split('|');
      return {
        key,
        category: category ?? '',
        severity: severity ?? '',
        isRecurrence: recurrence === 'true',
        recommendedRange: cell?.recommended_range ?? '',
      };
    });
  }

  private serialiseRows(): string {
    const cells: GridDefinition['cells'] = {};
    for (const row of this.rows()) {
      const key = `${row.category}|${row.severity}|${row.isRecurrence}`;
      cells[key] = { recommended_range: row.recommendedRange };
    }
    return JSON.stringify({ cells } satisfies GridDefinition);
  }

  updateRange(key: string, value: string): void {
    this.rows.update((rows) =>
      rows.map((r) => (r.key === key ? { ...r, recommendedRange: value } : r)),
    );
  }

  removeRow(key: string): void {
    this.rows.update((rows) => rows.filter((r) => r.key !== key));
  }

  addRow(): void {
    const category = this.newCategory.trim();
    const severity = this.newSeverity.trim();
    if (!category || !severity) {
      this.notify.warning(
        this.i18n.translate('sanctions.grid.categorySeverityRequired'),
      );
      return;
    }
    const key = `${category}|${severity}|${this.newRecurrence}`;
    if (this.rows().some((r) => r.key === key)) {
      this.notify.warning(this.i18n.translate('sanctions.grid.cellExists'));
      return;
    }
    this.rows.update((rows) => [
      ...rows,
      {
        key,
        category,
        severity,
        isRecurrence: this.newRecurrence,
        recommendedRange: '',
      },
    ]);
    this.newCategory = '';
    this.newSeverity = '';
    this.newRecurrence = false;
  }

  saveDraft(): void {
    this.pendingBanner.set(null);
    this.saving.set(true);
    this.service
      .saveGrid({ gridDefinition: this.serialiseRows() })
      .subscribe({
        next: (result) => {
          this.saving.set(false);
          if (result.pendingActionId) {
            this.pendingBanner.set(
              this.i18n.translate('sanctions.grid.savedPending', {
                action: result.pendingActionId,
              }),
            );
            this.notify.info(this.i18n.translate('sanctions.grid.draftSubmitted'));
          } else {
            this.notify.success(this.i18n.translate('sanctions.grid.draftSaved'));
            if (result.gridVersion) {
              this.grid.set(result.gridVersion);
              this.rows.set(
                this.parseRows(result.gridVersion.gridDefinitionJson),
              );
            }
            this.state.set('ready');
          }
        },
        error: () => this.saving.set(false),
      });
  }

  activate(): void {
    const g = this.grid();
    if (!g) {
      return;
    }
    const data: SanctionsReasonDialogData = {
      title: this.i18n.translate('sanctions.grid.activateTitle'),
      message: this.i18n.translate('sanctions.grid.activateMessage', {
        version: g.versionNumber,
      }),
      label: this.i18n.translate('sanctions.grid.activationReason'),
      minLength: 20,
      confirmLabel: this.i18n.translate('sanctions.grid.activate'),
    };
    this.dialog
      .open(SanctionsReasonDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: SanctionsReasonResult) => {
        if (!result) {
          return;
        }
        this.pendingBanner.set(null);
        this.service
          .activateGrid(g.id, { activationReason: result.reason })
          .subscribe({
            next: (action) => {
              if (action.pendingActionId) {
                this.pendingBanner.set(
                  this.i18n.translate('sanctions.grid.activationPending', {
                    action: action.pendingActionId,
                  }),
                );
                this.notify.info(
                  this.i18n.translate('sanctions.grid.activationSubmitted'),
                );
              } else {
                this.notify.success(
                  this.i18n.translate('sanctions.grid.versionActivated'),
                );
                if (action.gridVersion) {
                  this.grid.set(action.gridVersion);
                }
              }
            },
          });
      });
  }
}
