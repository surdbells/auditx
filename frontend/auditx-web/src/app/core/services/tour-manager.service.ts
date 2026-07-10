import { Injectable, computed, inject, signal } from '@angular/core';

import { PageGuide, TourStep } from '../models/page-guide.models';
import { TranslationService } from '../i18n/translation.service';

const STORAGE_PREFIX = 'auditx.guide.completed.';

/**
 * Drives the contextual page guide (a custom, dependency-free, theme-matched walkthrough — the Intro.js concept
 * without the AGPL package). Reusable on every page with one line: build the {@link TourStep} sequence from a page's
 * {@link PageGuide} metadata, track per-page completion in localStorage (so a first-time tour auto-runs exactly once),
 * and expose the active tour + navigation as signals the overlay renders. Localisation-ready: every string is resolved
 * through the {@link TranslationService} at build time.
 */
@Injectable({ providedIn: 'root' })
export class TourManagerService {
  private readonly i18n = inject(TranslationService);

  private readonly _steps = signal<TourStep[]>([]);
  private readonly _index = signal(0);
  private readonly _activeGuideId = signal<string | null>(null);

  /** The id of the guide whose tour is currently running (null when idle). */
  readonly activeGuideId = this._activeGuideId.asReadonly();
  readonly steps = this._steps.asReadonly();
  readonly index = this._index.asReadonly();
  readonly stepCount = computed(() => this._steps().length);
  readonly currentStep = computed<TourStep | null>(() => this._steps()[this._index()] ?? null);
  readonly isFirst = computed(() => this._index() === 0);
  readonly isLast = computed(() => this._index() >= this._steps().length - 1);

  /** True when the page's first-time tour has already been completed/dismissed on this device. */
  hasCompleted(guideId: string): boolean {
    try {
      return localStorage.getItem(STORAGE_PREFIX + guideId) === '1';
    } catch {
      return false;
    }
  }

  private markCompleted(guideId: string): void {
    try {
      localStorage.setItem(STORAGE_PREFIX + guideId, '1');
    } catch {
      // Private mode / storage disabled — the tour simply auto-runs next time; non-fatal.
    }
  }

  /** Starts (or restarts) the walkthrough for a page. */
  start(guide: PageGuide): void {
    const steps = this.buildSteps(guide);
    if (steps.length === 0) {
      return;
    }
    this._steps.set(steps);
    this._index.set(0);
    this._activeGuideId.set(guide.id);
  }

  next(): void {
    if (this.isLast()) {
      this.finish();
      return;
    }
    this._index.update((i) => i + 1);
  }

  previous(): void {
    if (!this.isFirst()) {
      this._index.update((i) => i - 1);
    }
  }

  goTo(index: number): void {
    if (index >= 0 && index < this._steps().length) {
      this._index.set(index);
    }
  }

  /** Completes the tour (marks it done so it won't auto-run again) and closes the overlay. */
  finish(): void {
    const id = this._activeGuideId();
    if (id) {
      this.markCompleted(id);
    }
    this.close();
  }

  /** Dismisses the tour WITHOUT marking it complete — used for Skip so the user can be re-offered it. */
  skip(): void {
    const id = this._activeGuideId();
    // Skipping still counts as "seen" — a first-time tour should not nag on every visit.
    if (id) {
      this.markCompleted(id);
    }
    this.close();
  }

  private close(): void {
    this._activeGuideId.set(null);
    this._steps.set([]);
    this._index.set(0);
  }

  /** Builds the standard 9-part sequence from metadata, resolving i18n and dropping empty sections. */
  private buildSteps(guide: PageGuide): TourStep[] {
    const t = (key: string): string => this.i18n.translate(key);
    const list = (keys?: string[]): string[] => (keys ?? []).map(t).filter((s) => s.length > 0);
    const steps: TourStep[] = [];

    // 1 — Welcome (purpose + business value).
    steps.push({
      icon: 'waving_hand',
      title: t(guide.titleKey),
      body: [t(guide.purposeKey), t(guide.descriptionKey)].filter(Boolean).join('\n\n'),
    });

    // 2 — What can be done here.
    const actions = list(guide.actionKeys);
    if (actions.length) {
      steps.push({ icon: 'checklist', title: t('guide.step.actions'), body: t('guide.step.actionsBody'), bullets: actions });
    }

    // 3 — Important sections (one spotlight step per present section).
    for (const section of guide.sections ?? []) {
      steps.push({ icon: 'ads_click', selector: section.selector, title: t(section.titleKey), body: t(section.bodyKey) });
    }

    // 4 — Process flow.
    const workflow = list(guide.workflowKeys);
    if (workflow.length) {
      steps.push({ icon: 'account_tree', title: t('guide.step.workflow'), body: t('guide.step.workflowBody'), bullets: workflow });
    }

    // 5 — Dependencies (what this page needs first).
    const dependsOn = list(guide.dependsOnKeys);
    if (dependsOn.length) {
      steps.push({ icon: 'link', title: t('guide.step.dependencies'), body: t('guide.step.dependenciesBody'), bullets: dependsOn });
    }

    // 6 — Used by (downstream consumers).
    const usedBy = list(guide.usedByKeys);
    if (usedBy.length) {
      steps.push({ icon: 'hub', title: t('guide.step.usedBy'), body: t('guide.step.usedByBody'), bullets: usedBy });
    }

    // 7 — Business rules.
    const rules = list(guide.businessRuleKeys);
    if (rules.length) {
      steps.push({ icon: 'rule', title: t('guide.step.rules'), body: t('guide.step.rulesBody'), bullets: rules });
    }

    // 8 — Tips & best practices.
    const tips = list(guide.tipKeys);
    if (tips.length) {
      steps.push({ icon: 'lightbulb', title: t('guide.step.tips'), body: t('guide.step.tipsBody'), bullets: tips });
    }

    // 9 — Completion.
    steps.push({ icon: 'check_circle', title: t('guide.step.done'), body: t('guide.step.doneBody') });

    return steps;
  }
}
