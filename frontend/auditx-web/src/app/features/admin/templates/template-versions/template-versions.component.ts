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
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';

import { TemplatesService } from '../../../../core/services/templates.service';
import {
  TemplateDiff,
  TemplateVersionDetail,
  TemplateVersionSummary,
} from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-template-versions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
  ],
  templateUrl: './template-versions.component.html',
  styleUrl: './template-versions.component.scss',
})
export class TemplateVersionsComponent {
  readonly templateId = input.required<string>();

  private readonly templatesService = inject(TemplatesService);

  readonly state = signal<ViewState>('loading');
  readonly versions = signal<TemplateVersionSummary[]>([]);

  /** Currently expanded version detail. */
  readonly selectedDetail = signal<TemplateVersionDetail | null>(null);
  readonly detailLoading = signal(false);

  /** Diff comparison selections. */
  readonly diffFrom = signal<number | null>(null);
  readonly diffTo = signal<number | null>(null);
  readonly diff = signal<TemplateDiff | null>(null);
  readonly diffLoading = signal(false);

  readonly hasVersions = computed(() => this.versions().length > 0);
  readonly canDiff = computed(() => this.versions().length >= 2);

  constructor() {
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set('loading');
    this.templatesService.versions(this.templateId()).subscribe({
      next: (versions) => {
        const sorted = [...versions].sort(
          (a, b) => b.versionNumber - a.versionNumber,
        );
        this.versions.set(sorted);
        if (sorted.length >= 2) {
          this.diffFrom.set(sorted[1].versionNumber);
          this.diffTo.set(sorted[0].versionNumber);
        }
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  viewVersion(n: number): void {
    if (this.selectedDetail()?.versionNumber === n) {
      this.selectedDetail.set(null);
      return;
    }
    this.detailLoading.set(true);
    this.templatesService.version(this.templateId(), n).subscribe({
      next: (detail) => {
        this.selectedDetail.set(detail);
        this.detailLoading.set(false);
      },
      error: () => this.detailLoading.set(false),
    });
  }

  compare(): void {
    const a = this.diffFrom();
    const b = this.diffTo();
    if (a === null || b === null || a === b) {
      return;
    }
    this.diffLoading.set(true);
    this.diff.set(null);
    this.templatesService.diff(this.templateId(), a, b).subscribe({
      next: (diff) => {
        this.diff.set(diff);
        this.diffLoading.set(false);
      },
      error: () => this.diffLoading.set(false),
    });
  }
}
