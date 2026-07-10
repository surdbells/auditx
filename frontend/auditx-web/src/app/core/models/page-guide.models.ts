/**
 * Metadata that drives a page's contextual guide (the Intro.js-style walkthrough + the "About this page" panel +
 * the relationship graph). One object per page; the Tour Manager generates the step sequence and the panel from it,
 * so adding a guide to a new page is pure data. All human-facing strings are i18n keys resolved through the `t` pipe /
 * TranslationService, so guides are localisable.
 */
export interface PageGuide {
  /** Stable id — the localStorage key for "tour completed" + the auto-first-run gate. Never reuse across pages. */
  id: string;
  /** i18n key: the page name. */
  titleKey: string;
  /** i18n key: one-line purpose ("why this page exists"). */
  purposeKey: string;
  /** i18n key: a short paragraph describing the page + its business value. */
  descriptionKey: string;

  /** i18n keys: the major actions available here (Create / Edit / Filter / Export …). */
  actionKeys?: string[];

  /**
   * Optional UI-section highlights. Each targets a live element by CSS selector and explains it in-place (step 3 of
   * the walkthrough). Sections whose element is absent at runtime are skipped, so a guide never breaks a page.
   */
  sections?: GuideSection[];

  /** i18n keys: the upstream business-process steps leading into this page. */
  workflowKeys?: string[];
  /** i18n keys: modules/data this page depends on (prerequisites). */
  dependsOnKeys?: string[];
  /** i18n keys: modules that consume this page's data (downstream). */
  usedByKeys?: string[];
  /** i18n keys: validation / business rules. */
  businessRuleKeys?: string[];
  /** i18n keys: tips & best practices. */
  tipKeys?: string[];
  /** i18n keys: roles/permissions that can use this page. */
  permissionKeys?: string[];
  /** FAQ entries (question + answer i18n keys). */
  faq?: GuideFaq[];
}

/** A highlighted UI region for the walkthrough. */
export interface GuideSection {
  /** CSS selector for the element to spotlight (first match). */
  selector: string;
  /** i18n key: the section's short title. */
  titleKey: string;
  /** i18n key: what it is / how to use it / its impact. */
  bodyKey: string;
}

export interface GuideFaq {
  questionKey: string;
  answerKey: string;
}

/** A resolved, ready-to-render walkthrough step (produced by the Tour Manager from a {@link PageGuide}). */
export interface TourStep {
  /** Optional element to spotlight; when absent the step is a centered modal. */
  selector?: string;
  /** Resolved title text. */
  title: string;
  /** Resolved body — may contain simple newline-separated lines / bullet items. */
  body: string;
  /** Bullet items (already resolved) rendered as a list under the body. */
  bullets?: string[];
  /** An icon (Material symbol) for the step header. */
  icon: string;
}
