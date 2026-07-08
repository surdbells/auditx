import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { CoverageService } from '../../core/services/coverage.service';
import { UniverseService } from '../../core/services/universe.service';
import {
  CoverageMatrix,
  HighRiskGapRow,
  NotAuditedRow,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

type ReportState = 'idle' | 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-coverage',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './coverage.component.html',
  styleUrl: './coverage.component.scss',
})
export class CoverageComponent {
  private readonly service = inject(CoverageService);
  private readonly universe = inject(UniverseService);
  private readonly fb = inject(FormBuilder);

  /** Entity-type options for the coverage filters. */
  readonly entityTypes = signal<string[]>([]);

  constructor() {
    this.universe.entityTypes().subscribe({
      next: (types) => this.entityTypes.set(types),
      error: () => {
        // Non-fatal: the filters just fall back to "Any".
      },
    });
  }

  /* ---- Not-audited-since report ---- */
  readonly notAuditedForm = this.fb.nonNullable.group({
    months: [12, [Validators.required, Validators.min(1)]],
    entityType: [''],
  });
  readonly notAuditedState = signal<ReportState>('idle');
  readonly notAuditedRows = signal<NotAuditedRow[]>([]);
  readonly notAuditedColumns = ['name', 'entityType', 'lastAudited'];

  /* ---- High-risk-gaps report ---- */
  readonly highRiskForm = this.fb.nonNullable.group({
    months: [12, [Validators.required, Validators.min(1)]],
    entityType: [''],
  });
  readonly highRiskState = signal<ReportState>('idle');
  readonly highRiskRows = signal<HighRiskGapRow[]>([]);
  readonly highRiskColumns = ['name', 'entityType', 'residual', 'lastAudited'];

  /* ---- Coverage matrix ---- */
  readonly matrixForm = this.fb.nonNullable.group({
    window: [''],
  });
  readonly matrixState = signal<ReportState>('idle');
  readonly matrix = signal<CoverageMatrix | null>(null);

  runNotAudited(): void {
    if (this.notAuditedForm.invalid) {
      this.notAuditedForm.markAllAsTouched();
      return;
    }
    const { months, entityType } = this.notAuditedForm.getRawValue();
    this.notAuditedState.set('loading');
    this.service.notAuditedSince(Number(months), entityType || undefined).subscribe({
      next: (rows) => {
        this.notAuditedRows.set(rows);
        this.notAuditedState.set('ready');
      },
      error: () => this.notAuditedState.set('error'),
    });
  }

  runHighRisk(): void {
    if (this.highRiskForm.invalid) {
      this.highRiskForm.markAllAsTouched();
      return;
    }
    const { months, entityType } = this.highRiskForm.getRawValue();
    this.highRiskState.set('loading');
    this.service.highRiskGaps(Number(months), entityType || undefined).subscribe({
      next: (rows) => {
        this.highRiskRows.set(rows);
        this.highRiskState.set('ready');
      },
      error: () => this.highRiskState.set('error'),
    });
  }

  runMatrix(): void {
    const { window } = this.matrixForm.getRawValue();
    this.matrixState.set('loading');
    this.service.matrix(window || undefined).subscribe({
      next: (matrix) => {
        this.matrix.set(matrix);
        this.matrixState.set('ready');
      },
      error: () => this.matrixState.set('error'),
    });
  }
}
