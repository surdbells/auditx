import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { IconComponent } from '../../../../core/icons/icon.component';
import { ResponseOptionSetsService } from '../../../../core/services/response-option-sets.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { ResponseOption, ResponseOptionSet } from '../../../../core/models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

interface EditableSet {
  responseType: string;
  isCustomised: boolean;
  version: string | null;
  options: ResponseOption[];
}

/**
 * Admin screen for organisation-defined conclusion labels: relabel and re-score the options an auditor picks for
 * each verdict-based response type (e.g. Compliant / Partially Compliant / Non-Compliant / N-A). The engine derives
 * a canonical Pass/Fail/N-A verdict from each option's semantics, so scoring, findings and reports keep working.
 */
@Component({
  selector: 'app-response-labels',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
    IconComponent,
    PageHeaderComponent,
    TranslatePipe,
  ],
  template: `
    <app-page-header [title]="'responseLabels.title' | t" [subtitle]="'responseLabels.subtitle' | t" />

    @for (set of sets(); track set.responseType) {
      <mat-card appearance="outlined" class="set">
        <div class="set__head">
          <h2 class="set__title">
            {{ 'responseLabels.type.' + set.responseType | t }}
            @if (!set.isCustomised) {
              <span class="set__default">{{ 'responseLabels.default' | t }}</span>
            }
          </h2>
        </div>

        <div class="table-scroll">
          <table class="opts">
            <thead>
              <tr>
                <th>{{ 'responseLabels.col.label' | t }}</th>
                <th class="num">{{ 'responseLabels.col.score' | t }}</th>
                <th class="chk">{{ 'responseLabels.col.deficiency' | t }}</th>
                <th class="chk">{{ 'responseLabels.col.na' | t }}</th>
                <th class="chk">{{ 'responseLabels.col.comment' | t }}</th>
                <th class="act"></th>
              </tr>
            </thead>
            <tbody>
              @for (o of set.options; track $index) {
                <tr>
                  <td>
                    <input class="cell-input" [(ngModel)]="o.label" [disabled]="!canManage()" [attr.aria-label]="'responseLabels.col.label' | t" />
                  </td>
                  <td class="num">
                    <input class="cell-input num" type="number" min="0" max="100" [ngModel]="o.score" (ngModelChange)="o.score = normaliseScore($event)" [disabled]="!canManage() || o.isNotApplicable" />
                  </td>
                  <td class="chk"><mat-checkbox [(ngModel)]="o.isDeficiency" [disabled]="!canManage()" /></td>
                  <td class="chk"><mat-checkbox [(ngModel)]="o.isNotApplicable" [disabled]="!canManage()" /></td>
                  <td class="chk"><mat-checkbox [(ngModel)]="o.requiresComment" [disabled]="!canManage()" /></td>
                  <td class="act">
                    @if (canManage()) {
                      <button matIconButton type="button" [disabled]="$index === 0" (click)="move(set, $index, -1)" [attr.aria-label]="'responseLabels.moveUp' | t"><app-icon name="arrow_upward" /></button>
                      <button matIconButton type="button" [disabled]="$index === set.options.length - 1" (click)="move(set, $index, 1)" [attr.aria-label]="'responseLabels.moveDown' | t"><app-icon name="arrow_downward" /></button>
                      <button matIconButton type="button" [disabled]="set.options.length <= 1" (click)="remove(set, $index)" [attr.aria-label]="'responseLabels.removeOption' | t"><app-icon name="delete" /></button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        @if (canManage()) {
          <div class="set__actions">
            <button matButton type="button" (click)="addOption(set)"><app-icon name="add" /> {{ 'responseLabels.addOption' | t }}</button>
            <span class="spacer"></span>
            @if (set.isCustomised) {
              <button matButton type="button" (click)="reset(set)"><app-icon name="refresh" /> {{ 'responseLabels.reset' | t }}</button>
            }
            <button matButton="filled" type="button" [disabled]="saving()" (click)="save(set)"><app-icon name="save" /> {{ 'responseLabels.save' | t }}</button>
          </div>
          <p class="hint">{{ 'responseLabels.hint' | t }}</p>
        }
      </mat-card>
    }
  `,
  styles: `
    .set {
      margin-bottom: 1.25rem;
      padding: 1rem 1.25rem;
    }
    .set__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 0.5rem;
    }
    .set__title {
      font-size: 1.05rem;
      margin: 0;
      display: flex;
      align-items: center;
      gap: 0.6rem;
    }
    .set__default {
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
      background: var(--mat-sys-surface-container-high);
      padding: 0.1rem 0.5rem;
      border-radius: 999px;
    }
    table.opts {
      width: 100%;
      border-collapse: collapse;
      min-width: 560px;
    }
    .opts th {
      text-align: left;
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
      padding: 0.35rem 0.5rem;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .opts td {
      padding: 0.25rem 0.5rem;
      border-bottom: 1px solid var(--mat-sys-surface-container-high);
      vertical-align: middle;
    }
    .opts th.num, .opts td.num { text-align: right; width: 5rem; }
    .opts th.chk, .opts td.chk { text-align: center; width: 5rem; }
    .opts td.act { text-align: right; white-space: nowrap; width: 8rem; }
    .cell-input {
      width: 100%;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 6px;
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      font: inherit;
      padding: 0.35rem 0.5rem;
    }
    .cell-input.num { text-align: right; width: 4.5rem; }
    .set__actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-top: 0.75rem;
    }
    .spacer { flex: 1; }
    .hint {
      margin: 0.5rem 0 0;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ResponseLabelsComponent {
  private readonly service = inject(ResponseOptionSetsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslationService);

  readonly sets = signal<EditableSet[]>([]);
  readonly saving = signal(false);

  canManage(): boolean {
    return this.auth.hasPermission(Permissions.ManageTemplates);
  }

  constructor() {
    this.service.list().subscribe((sets) => this.sets.set(sets.map((s) => this.toEditable(s))));
  }

  private toEditable(s: ResponseOptionSet): EditableSet {
    return {
      responseType: s.responseType,
      isCustomised: s.isCustomised,
      version: s.version,
      // Clone so edits don't mutate the source until saved.
      options: s.options.map((o) => ({ ...o })),
    };
  }

  normaliseScore(value: number | string | null): number | null {
    if (value === null || value === '' || value === undefined) {
      return null;
    }
    const n = Math.round(Number(value));
    if (Number.isNaN(n)) {
      return null;
    }
    return Math.min(100, Math.max(0, n));
  }

  move(set: EditableSet, index: number, delta: number): void {
    const target = index + delta;
    if (target < 0 || target >= set.options.length) {
      return;
    }
    const opts = [...set.options];
    [opts[index], opts[target]] = [opts[target], opts[index]];
    this.replace(set, { options: opts });
  }

  remove(set: EditableSet, index: number): void {
    const opts = set.options.filter((_, i) => i !== index);
    this.replace(set, { options: opts });
  }

  addOption(set: EditableSet): void {
    const opts = [
      ...set.options,
      { code: '', label: '', order: set.options.length, score: null, isDeficiency: false, isNotApplicable: false, requiresComment: false },
    ];
    this.replace(set, { options: opts });
  }

  save(set: EditableSet): void {
    const prepared = this.prepare(set.options);
    if (!prepared) {
      this.notify.error(this.i18n.translate('responseLabels.error.invalid'));
      return;
    }
    this.saving.set(true);
    this.service.update(set.responseType, { optionsJson: JSON.stringify(prepared) }).subscribe({
      next: (updated) => {
        this.replaceSet(updated);
        this.saving.set(false);
        this.notify.success(this.i18n.translate('responseLabels.notify.saved'));
      },
      error: () => this.saving.set(false),
    });
  }

  reset(set: EditableSet): void {
    this.service.reset(set.responseType).subscribe({
      next: (updated) => {
        this.replaceSet(updated);
        this.notify.success(this.i18n.translate('responseLabels.notify.reset'));
      },
    });
  }

  /** Assign order + derive stable codes; return null if any option lacks a label or there's no conclusive option. */
  private prepare(options: ResponseOption[]): ResponseOption[] | null {
    if (!options.length || options.some((o) => !o.label.trim()) || options.every((o) => o.isNotApplicable)) {
      return null;
    }
    const used = new Set<string>();
    return options.map((o, i) => {
      let code = (o.code || this.slug(o.label)).toLowerCase();
      let candidate = code;
      let n = 1;
      while (used.has(candidate)) {
        candidate = `${code}_${n++}`;
      }
      used.add(candidate);
      return {
        code: candidate,
        label: o.label.trim(),
        order: i,
        score: o.isNotApplicable ? null : this.normaliseScore(o.score),
        isDeficiency: o.isDeficiency,
        isNotApplicable: o.isNotApplicable,
        requiresComment: o.requiresComment,
      };
    });
  }

  private slug(label: string): string {
    return label.trim().toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '').slice(0, 40) || 'opt';
  }

  private replace(set: EditableSet, patch: Partial<EditableSet>): void {
    this.sets.update((all) => all.map((s) => (s.responseType === set.responseType ? { ...s, ...patch } : s)));
  }

  private replaceSet(updated: ResponseOptionSet): void {
    this.sets.update((all) => all.map((s) => (s.responseType === updated.responseType ? this.toEditable(updated) : s)));
  }
}
