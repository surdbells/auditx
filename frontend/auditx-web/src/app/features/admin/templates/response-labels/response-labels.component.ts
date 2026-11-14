import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { IconComponent } from '../../../../core/icons/icon.component';
import { ResponseOptionSetsService } from '../../../../core/services/response-option-sets.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { ResponseOption, ResponseOptionSet } from '../../../../core/models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

/** Contextual page guide for the response-labels (conclusion options) admin page. */
const RESPONSE_LABELS_GUIDE: PageGuide = {
  id: 'admin-response-labels',
  titleKey: 'responseLabels.title',
  purposeKey: 'responseLabels.guide.purpose',
  descriptionKey: 'responseLabels.guide.description',
  actionKeys: [
    'responseLabels.guide.action.relabel',
    'responseLabels.guide.action.score',
    'responseLabels.guide.action.reset',
    'responseLabels.guide.action.newSet',
    'responseLabels.guide.action.retire',
  ],
  tipKeys: [
    'responseLabels.guide.tip.verdict',
    'responseLabels.guide.tip.reports',
    'responseLabels.guide.tip.namedSets',
  ],
};

/** The verdict-based response types a set can be made for. */
const SET_TYPES = ['pass_fail_na', 'yes_no'] as const;

/** A set as edited on screen. `key` is stable across saves: the type for a standard set, the id (or a draft key) for a named one. */
interface EditableSet {
  key: string;
  responseType: string;
  /** Null for a response type's standard set. */
  id: string | null;
  /** Null for a response type's standard set. */
  name: string | null;
  isArchived: boolean;
  isCustomised: boolean;
  version: string | null;
  /** A named set not yet saved. */
  isNew: boolean;
  options: ResponseOption[];
}

/**
 * Admin screen for the conclusion options auditors pick on verdict-based items. Each response type has a STANDARD set
 * (relabel and re-score Pass / Fail / N-A or Yes / No) that every item of that type uses; NAMED sets are additional
 * vocabularies (e.g. "Control effectiveness": Effective / Partially effective / Ineffective) that an item can choose
 * instead. The engine derives a canonical Pass/Fail/N-A verdict from each option's semantics, so scoring, findings and
 * reports keep working whatever the wording.
 */
@Component({
  selector: 'app-response-labels',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgTemplateOutlet,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    MatButtonModule,
    IconComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  template: `
    <app-page-header [title]="'responseLabels.title' | t" [subtitle]="'responseLabels.subtitle' | t">
      @if (canManage()) {
        <button actions matButton="filled" type="button" (click)="newSet()" [disabled]="hasUnsavedNew()">
          <app-icon name="add" /> {{ 'responseLabels.newSet' | t }}
        </button>
      }
    </app-page-header>

    <app-page-guide [guide]="guide" />

    <h2 class="group-title">{{ 'responseLabels.standardSets' | t }}</h2>
    <p class="group-hint">{{ 'responseLabels.standardSetsHint' | t }}</p>
    @for (set of standardSets(); track set.key) {
      <ng-container [ngTemplateOutlet]="setCard" [ngTemplateOutletContext]="{ $implicit: set }" />
    }

    <div class="group-head">
      <div>
        <h2 class="group-title">{{ 'responseLabels.namedSets' | t }}</h2>
        <p class="group-hint">{{ 'responseLabels.namedSetsHint' | t }}</p>
      </div>
      @if (hasRetired()) {
        <mat-slide-toggle [checked]="showRetired()" (change)="showRetired.set($event.checked)">
          {{ 'responseLabels.showRetired' | t }}
        </mat-slide-toggle>
      }
    </div>
    @for (set of visibleNamedSets(); track set.key) {
      <ng-container [ngTemplateOutlet]="setCard" [ngTemplateOutletContext]="{ $implicit: set }" />
    } @empty {
      <p class="empty">{{ 'responseLabels.noNamedSets' | t }}</p>
    }

    <ng-template #setCard let-set>
      <mat-card appearance="outlined" class="set" [class.set--retired]="set.isArchived">
        <div class="set__head">
          @if (set.name === null) {
            <h3 class="set__title">
              {{ 'responseLabels.type.' + set.responseType | t }}
              @if (!set.isCustomised) {
                <span class="chip">{{ 'responseLabels.default' | t }}</span>
              }
            </h3>
          } @else {
            <div class="set__identity">
              <mat-form-field appearance="outline" class="set__name" subscriptSizing="dynamic">
                <mat-label>{{ 'responseLabels.field.name' | t }}</mat-label>
                <input matInput [(ngModel)]="set.name" maxlength="100" [disabled]="!canManage()" required />
              </mat-form-field>
              @if (set.isNew) {
                <mat-form-field appearance="outline" class="set__type" subscriptSizing="dynamic">
                  <mat-label>{{ 'responseLabels.field.type' | t }}</mat-label>
                  <mat-select [(ngModel)]="set.responseType">
                    @for (t of setTypes; track t) {
                      <mat-option [value]="t">{{ 'responseLabels.type.' + t | t }}</mat-option>
                    }
                  </mat-select>
                  <mat-hint>{{ 'responseLabels.field.typeHint' | t }}</mat-hint>
                </mat-form-field>
              } @else {
                <span class="chip">{{ 'responseLabels.type.' + set.responseType | t }}</span>
              }
              @if (set.isArchived) {
                <span class="chip chip--retired">{{ 'responseLabels.retired' | t }}</span>
              }
            </div>
          }
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
                    <input class="cell-input num" type="number" min="0" max="100" [ngModel]="o.score" (ngModelChange)="o.score = normaliseScore($event)" [disabled]="!canManage() || o.isNotApplicable" [attr.aria-label]="'responseLabels.col.score' | t" />
                  </td>
                  <td class="chk"><mat-checkbox [(ngModel)]="o.isDeficiency" [disabled]="!canManage()" [attr.aria-label]="'responseLabels.col.deficiency' | t" /></td>
                  <td class="chk"><mat-checkbox [(ngModel)]="o.isNotApplicable" [disabled]="!canManage()" [attr.aria-label]="'responseLabels.col.na' | t" /></td>
                  <td class="chk"><mat-checkbox [(ngModel)]="o.requiresComment" [disabled]="!canManage()" [attr.aria-label]="'responseLabels.col.comment' | t" /></td>
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
            @if (set.name === null && set.isCustomised) {
              <button matButton type="button" (click)="reset(set)"><app-icon name="refresh" /> {{ 'responseLabels.reset' | t }}</button>
            }
            @if (set.isNew) {
              <button matButton type="button" (click)="discardNew(set)">{{ 'common.cancel' | t }}</button>
            } @else if (set.name !== null) {
              @if (set.isArchived) {
                <button matButton type="button" (click)="setRetired(set, false)"><app-icon name="unarchive" /> {{ 'responseLabels.restore' | t }}</button>
              } @else {
                <button matButton type="button" (click)="setRetired(set, true)"><app-icon name="archive" /> {{ 'responseLabels.retire' | t }}</button>
              }
            }
            <button matButton="filled" type="button" [disabled]="saving()" (click)="save(set)"><app-icon name="save" /> {{ 'responseLabels.save' | t }}</button>
          </div>
          <p class="hint">{{ 'responseLabels.hint' | t }}</p>
        }
      </mat-card>
    </ng-template>
  `,
  styles: `
    .group-title {
      font-size: 1.05rem;
      margin: 1.25rem 0 0.15rem;
    }
    .group-hint {
      margin: 0 0 0.75rem;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .group-head {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .empty {
      margin: 0 0 1.25rem;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }
    .set {
      margin-bottom: 1.25rem;
      padding: 1rem 1.25rem;
    }
    .set--retired {
      opacity: 0.8;
    }
    .set__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 0.5rem;
    }
    .set__title {
      font-size: 1rem;
      margin: 0;
      display: flex;
      align-items: center;
      gap: 0.6rem;
    }
    .set__identity {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-wrap: wrap;
      width: 100%;
    }
    .set__name {
      flex: 1 1 18rem;
      max-width: 28rem;
    }
    .set__type {
      flex: 0 1 16rem;
    }
    .chip {
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
      background: var(--mat-sys-surface-container-high);
      padding: 0.15rem 0.55rem;
      border-radius: 999px;
      white-space: nowrap;
    }
    .chip--retired {
      color: var(--mat-sys-on-error-container);
      background: var(--mat-sys-error-container);
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
      flex-wrap: wrap;
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

  readonly guide = RESPONSE_LABELS_GUIDE;
  readonly setTypes = SET_TYPES;

  readonly sets = signal<EditableSet[]>([]);
  readonly saving = signal(false);
  readonly showRetired = signal(false);
  private draftCounter = 0;

  readonly standardSets = computed(() => this.sets().filter((s) => s.name === null && !s.isNew));
  private readonly namedSets = computed(() => this.sets().filter((s) => s.name !== null || s.isNew));
  readonly visibleNamedSets = computed(() =>
    this.namedSets().filter((s) => this.showRetired() || !s.isArchived),
  );
  readonly hasRetired = computed(() => this.namedSets().some((s) => s.isArchived));
  readonly hasUnsavedNew = computed(() => this.sets().some((s) => s.isNew));

  canManage(): boolean {
    return this.auth.hasPermission(Permissions.ManageTemplates);
  }

  constructor() {
    this.service.list().subscribe((sets) => this.sets.set(sets.map((s) => this.toEditable(s))));
  }

  private toEditable(s: ResponseOptionSet): EditableSet {
    return {
      key: s.id ?? s.responseType,
      responseType: s.responseType,
      id: s.id ?? null,
      name: s.name ?? null,
      isArchived: !!s.isArchived,
      isCustomised: s.isCustomised,
      version: s.version,
      isNew: false,
      // Clone so edits don't mutate the source until saved.
      options: s.options.map((o) => ({ ...o })),
    };
  }

  /** Starts a new named set, pre-filled with a three-level vocabulary the administrator then words their own way. */
  newSet(): void {
    const draft: EditableSet = {
      key: `new-${++this.draftCounter}`,
      responseType: 'pass_fail_na',
      id: null,
      name: '',
      isArchived: false,
      isCustomised: true,
      version: null,
      isNew: true,
      options: [
        { code: '', label: '', order: 0, score: 100, isDeficiency: false, isNotApplicable: false, requiresComment: false },
        { code: '', label: '', order: 1, score: 0, isDeficiency: true, isNotApplicable: false, requiresComment: true },
        { code: '', label: '', order: 2, score: null, isDeficiency: false, isNotApplicable: true, requiresComment: false },
      ],
    };
    this.sets.update((all) => [...all, draft]);
  }

  discardNew(set: EditableSet): void {
    this.sets.update((all) => all.filter((s) => s.key !== set.key));
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
    const name = set.name?.trim() ?? null;
    if (set.name !== null && !name) {
      this.notify.error(this.i18n.translate('responseLabels.error.nameRequired'));
      return;
    }

    const optionsJson = JSON.stringify(prepared);
    const request$ = set.isNew
      ? this.service.createNamed({ name: name!, responseType: set.responseType, optionsJson })
      : set.id
        ? this.service.updateNamed(set.id, { name: name!, optionsJson, version: set.version })
        : this.service.update(set.responseType, { optionsJson });

    this.saving.set(true);
    request$.subscribe({
      next: (updated) => {
        this.replaceWith(set.key, updated);
        this.saving.set(false);
        this.notify.success(this.i18n.translate('responseLabels.notify.saved'));
      },
      error: () => this.saving.set(false),
    });
  }

  reset(set: EditableSet): void {
    this.service.reset(set.responseType).subscribe({
      next: (updated) => {
        this.replaceWith(set.key, updated);
        this.notify.success(this.i18n.translate('responseLabels.notify.reset'));
      },
    });
  }

  /** Retire a named set (no longer offered for new items; items already using it keep it) or bring it back. */
  setRetired(set: EditableSet, retired: boolean): void {
    if (!set.id) {
      return;
    }
    const request$ = retired ? this.service.retire(set.id) : this.service.restore(set.id);
    request$.subscribe({
      next: (updated) => {
        this.replaceWith(set.key, updated);
        this.notify.success(
          this.i18n.translate(retired ? 'responseLabels.notify.retired' : 'responseLabels.notify.restored', {
            name: updated.name ?? '',
          }),
        );
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
      const code = (o.code || this.slug(o.label)).toLowerCase();
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
    this.sets.update((all) => all.map((s) => (s.key === set.key ? { ...s, ...patch } : s)));
  }

  private replaceWith(key: string, updated: ResponseOptionSet): void {
    this.sets.update((all) => all.map((s) => (s.key === key ? this.toEditable(updated) : s)));
  }
}
