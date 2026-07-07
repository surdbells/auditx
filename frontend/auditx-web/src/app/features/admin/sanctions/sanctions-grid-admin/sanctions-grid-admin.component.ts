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
import {
  GridDefinition,
  SanctionsGridVersion,
} from '../../../../core/models';
import { humanise } from '../humanise';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import {
  SanctionsReasonDialogComponent,
  SanctionsReasonDialogData,
  SanctionsReasonResult,
} from '../dialogs/sanctions-reason-dialog.component';

type ViewState = 'loading' | 'ready' | 'error' | 'empty';

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
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './sanctions-grid-admin.component.html',
  styleUrl: './sanctions-grid-admin.component.scss',
})
export class SanctionsGridAdminComponent {
  private readonly service = inject(SanctionsService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);

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
      this.notify.warning('Category and severity are required.');
      return;
    }
    const key = `${category}|${severity}|${this.newRecurrence}`;
    if (this.rows().some((r) => r.key === key)) {
      this.notify.warning('That category / severity / recurrence cell exists.');
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
              `Saved as a draft awaiting a second approver (action ${result.pendingActionId}).`,
            );
            this.notify.info('Grid draft submitted for sign-off.');
          } else {
            this.notify.success('Grid draft saved.');
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
      title: 'Activate grid version',
      message: `Activate grid v${g.versionNumber}. Provide an activation reason (at least 20 characters).`,
      label: 'Activation reason',
      minLength: 20,
      confirmLabel: 'Activate',
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
                  `Activation awaiting a second approver (action ${action.pendingActionId}).`,
                );
                this.notify.info('Activation submitted for sign-off.');
              } else {
                this.notify.success('Grid version activated.');
                if (action.gridVersion) {
                  this.grid.set(action.gridVersion);
                }
              }
            },
          });
      });
  }
}
