import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  CdkDropList,
  CdkDrag,
  CdkDragHandle,
  type CdkDragDrop,
  moveItemInArray,
  transferArrayItem,
} from '@angular/cdk/drag-drop';
import { switchMap } from 'rxjs';

import { TemplatesService } from '../../../../core/services/templates.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ReferenceDataLookupService } from '../../../../core/services/reference-data-lookup.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Permissions } from '../../../../core/permissions';
import {
  Template,
  TemplateItem,
} from '../../../../core/models';
import {
  ItemEditorDialogComponent,
  ItemEditorDialogData,
} from '../dialogs/item-editor-dialog.component';
import {
  SectionDialogComponent,
  SectionDialogData,
} from '../dialogs/section-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { TemplateVersionsComponent } from '../template-versions/template-versions.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** A section grouping with its ordered items, for rendering. */
interface SectionGroup {
  name: string;
  items: TemplateItem[];
}

/** Contextual page guide for the checklist template editor (drives the walkthrough + the About panel). */
const TEMPLATE_EDITOR_GUIDE: PageGuide = {
  id: 'template-editor',
  titleKey: 'templatesAdmin.editor.guideTitle',
  purposeKey: 'templatesAdmin.editor.guide.purpose',
  descriptionKey: 'templatesAdmin.editor.guide.description',
  actionKeys: [
    'templatesAdmin.editor.guide.action.sections',
    'templatesAdmin.editor.guide.action.items',
    'templatesAdmin.editor.guide.action.reorder',
    'templatesAdmin.editor.guide.action.publish',
  ],
  sections: [
    { selector: '.editor__form', titleKey: 'templatesAdmin.editor.guide.section.details.title', bodyKey: 'templatesAdmin.editor.guide.section.details.body' },
    { selector: '.editor__sections', titleKey: 'templatesAdmin.editor.guide.section.structure.title', bodyKey: 'templatesAdmin.editor.guide.section.structure.body' },
    { selector: '.editor__lifecycle', titleKey: 'templatesAdmin.editor.guide.section.lifecycle.title', bodyKey: 'templatesAdmin.editor.guide.section.lifecycle.body' },
  ],
  workflowKeys: ['templatesAdmin.editor.guide.flow.draft', 'templatesAdmin.editor.guide.flow.build', 'templatesAdmin.editor.guide.flow.publish', 'templatesAdmin.editor.guide.flow.copy'],
  dependsOnKeys: ['templatesAdmin.editor.guide.dep.auditTypes'],
  usedByKeys: ['templatesAdmin.editor.guide.use.audits'],
  businessRuleKeys: ['templatesAdmin.editor.guide.rule.draftOnly', 'templatesAdmin.editor.guide.rule.immutableType', 'templatesAdmin.editor.guide.rule.newDraft'],
  tipKeys: ['templatesAdmin.editor.guide.tip.dragDrop'],
  permissionKeys: ['templatesAdmin.editor.guide.perm.manage'],
  faq: [
    { questionKey: 'templatesAdmin.editor.guide.faq.editPublished.q', answerKey: 'templatesAdmin.editor.guide.faq.editPublished.a' },
  ],
};

@Component({
  selector: 'app-template-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    IconComponent,
    MatMenuModule,
    MatSelectModule,
    MatChipsModule,
    MatTooltipModule,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TemplateVersionsComponent,
  ],
  templateUrl: './template-editor.component.html',
  styleUrl: './template-editor.component.scss',
})
export class TemplateEditorComponent {
  /** Route param: 'new' for create, otherwise the template id. */
  readonly id = input<string>('new');

  private readonly templatesService = inject(TemplatesService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);
  private readonly refLookup = inject(ReferenceDataLookupService);

  /** Active audit-type reference-data items (lazy-loaded). */
  readonly auditTypes = this.refLookup.options('audit_type');

  readonly state = signal<ViewState>('loading');
  readonly saving = signal(false);
  readonly template = signal<Template | null>(null);

  // Treat a missing/empty route param the same as the explicit 'new' sentinel. Angular's
  // withComponentInputBinding() does not preserve the input default on the paramless
  // `/admin/templates/new` route (it pushes undefined), so relying on `=== 'new'` alone
  // sent create-mode down the load-by-id path → GET /templates/undefined → 404 error page.
  readonly isNew = computed(() => {
    const id = this.id();
    return !id || id === 'new';
  });
  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageTemplates),
  );

  readonly isDraft = computed(() => this.template()?.status === 'draft');
  readonly isPublished = computed(() => this.template()?.status === 'published');
  readonly isArchived = computed(() => this.template()?.status === 'archived');

  /** Metadata is editable only on a draft by a user who can manage templates. */
  readonly canEditMetadata = computed(
    () => this.canManage() && (this.isNew() || this.isDraft()),
  );

  /** Items/sections are mutable only on a draft. */
  readonly canEditStructure = computed(
    () => this.canManage() && this.isDraft(),
  );

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(160)]],
    auditType: ['', [Validators.required, Validators.maxLength(80)]],
    description: ['', [Validators.maxLength(1000)]],
  });

  /** Items grouped by section, in section order, with un-sectioned items last. */
  readonly sectionGroups = computed<SectionGroup[]>(() => {
    const t = this.template();
    if (!t) {
      return [];
    }
    const sorted = [...t.items].sort((a, b) => a.orderIndex - b.orderIndex);
    const groups: SectionGroup[] = t.sections
      .slice()
      .sort((a, b) => a.orderIndex - b.orderIndex)
      .map((s) => ({
        name: s.name,
        items: sorted.filter((i) => i.sectionName === s.name),
      }));
    const sectionNames = new Set(t.sections.map((s) => s.name));
    const orphans = sorted.filter((i) => !sectionNames.has(i.sectionName));
    if (orphans.length) {
      groups.push({ name: '', items: orphans });
    }
    return groups;
  });

  readonly sectionNames = computed(() =>
    (this.template()?.sections ?? []).map((s) => s.name),
  );

  /** Ordered list of items across all sections (for reorder bounds). */
  readonly orderedItems = computed(() =>
    [...(this.template()?.items ?? [])].sort(
      (a, b) => a.orderIndex - b.orderIndex,
    ),
  );

  readonly guide = TEMPLATE_EDITOR_GUIDE;

  constructor() {
    queueMicrotask(() => this.bootstrap());
  }

  private bootstrap(): void {
    if (this.isNew()) {
      this.state.set('ready');
      return;
    }
    this.state.set('loading');
    this.templatesService.getById(this.id()).subscribe({
      next: (template) => {
        this.applyTemplate(template);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  private applyTemplate(template: Template): void {
    this.template.set(template);
    this.form.patchValue({
      name: template.name,
      auditType: template.auditType,
      description: template.description,
    });
    if (!this.canEditMetadata()) {
      this.form.disable();
    } else {
      this.form.enable();
      // auditType is immutable after creation.
      if (!this.isNew()) {
        this.form.controls.auditType.disable();
      }
    }
  }

  cancel(): void {
    void this.router.navigate(['/admin/templates']);
  }

  /* ---- Metadata ---- */

  saveMetadata(): void {
    if (!this.canEditMetadata() || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    if (this.isNew()) {
      this.templatesService
        .create({
          name: v.name.trim(),
          auditType: v.auditType.trim(),
          description: v.description.trim(),
        })
        .subscribe({
          next: (created) => {
            this.saving.set(false);
            this.notify.success(this.i18n.translate('templatesAdmin.notify.created'));
            void this.router.navigate(['/admin/templates', created.id]);
          },
          error: () => this.saving.set(false),
        });
    } else {
      this.templatesService
        .update(this.id(), {
          name: v.name.trim(),
          description: v.description.trim(),
        })
        .subscribe({
          next: (updated) => {
            this.saving.set(false);
            this.applyTemplate(updated);
            this.notify.success(this.i18n.translate('templatesAdmin.notify.updated'));
          },
          error: () => this.saving.set(false),
        });
    }
  }

  /* ---- Sections ---- */

  addSection(): void {
    const data: SectionDialogData = {
      title: this.i18n.translate('templatesAdmin.editor.addSection'),
      confirmLabel: this.i18n.translate('templatesAdmin.action.add'),
    };
    this.dialog
      .open(SectionDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((name) => {
        if (!name) {
          return;
        }
        this.templatesService.addSection(this.id(), { name }).subscribe({
          next: (t) => {
            this.applyTemplate(t);
            this.notify.success(this.i18n.translate('templatesAdmin.notify.sectionAdded'));
          },
        });
      });
  }

  renameSection(currentName: string): void {
    const data: SectionDialogData = {
      title: this.i18n.translate('templatesAdmin.editor.renameSection'),
      confirmLabel: this.i18n.translate('templatesAdmin.action.rename'),
      name: currentName,
    };
    this.dialog
      .open(SectionDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((newName) => {
        if (!newName || newName === currentName) {
          return;
        }
        this.templatesService
          .renameSection(this.id(), { currentName, newName })
          .subscribe({
            next: () => {
              this.notify.success(this.i18n.translate('templatesAdmin.notify.sectionRenamed'));
              this.refresh();
            },
          });
      });
  }

  removeSection(name: string): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('templatesAdmin.editor.removeSection'),
      message: this.i18n.translate('templatesAdmin.editor.removeSectionMessage', {
        name,
      }),
      confirmLabel: this.i18n.translate('templatesAdmin.action.remove'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.templatesService.removeSection(this.id(), name).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('templatesAdmin.notify.sectionRemoved'));
            this.refresh();
          },
        });
      });
  }

  /* ---- Items ---- */

  addItem(): void {
    const data: ItemEditorDialogData = {
      item: null,
      sections: this.sectionNames(),
    };
    this.dialog
      .open(ItemEditorDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((body) => {
        if (!body) {
          return;
        }
        this.templatesService.addItem(this.id(), body).subscribe({
          next: (t) => {
            this.applyTemplate(t);
            this.notify.success(this.i18n.translate('templatesAdmin.notify.itemAdded'));
          },
        });
      });
  }

  editItem(item: TemplateItem): void {
    const data: ItemEditorDialogData = {
      item,
      sections: this.sectionNames(),
    };
    this.dialog
      .open(ItemEditorDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((body) => {
        if (!body) {
          return;
        }
        this.templatesService.updateItem(this.id(), item.id, body).subscribe({
          next: (t) => {
            this.applyTemplate(t);
            this.notify.success(this.i18n.translate('templatesAdmin.notify.itemUpdated'));
          },
        });
      });
  }

  removeItem(item: TemplateItem): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('templatesAdmin.editor.removeItem'),
      message: this.i18n.translate('templatesAdmin.editor.removeItemMessage'),
      confirmLabel: this.i18n.translate('templatesAdmin.action.remove'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.templatesService.removeItem(this.id(), item.id).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('templatesAdmin.notify.itemRemoved'));
            this.refresh();
          },
        });
      });
  }

  moveItem(item: TemplateItem, direction: -1 | 1): void {
    const ordered = this.orderedItems();
    const index = ordered.findIndex((i) => i.id === item.id);
    const target = index + direction;
    if (index === -1 || target < 0 || target >= ordered.length) {
      return;
    }
    const reordered = [...ordered];
    [reordered[index], reordered[target]] = [
      reordered[target],
      reordered[index],
    ];
    const orderedItemIds = reordered.map((i) => i.id);
    this.templatesService
      .reorderItems(this.id(), { orderedItemIds })
      .subscribe({
        next: () => this.refresh(),
      });
  }

  /* ---- Drag & drop reordering ---- */

  /** Stable cdkDropList id for a section (empty name = the un-sectioned group). */
  listId(sectionName: string): string {
    return `dl:${sectionName}`;
  }

  private sectionFromListId(id: string): string {
    return id.startsWith('dl:') ? id.slice(3) : id;
  }

  /** All section drop-list ids, so every list is a connected drop target for cross-section moves. */
  readonly dropListIds = computed(() =>
    this.sectionGroups().map((g) => this.listId(g.name)),
  );

  /**
   * Handle an item dropped within or across sections. Optimistically reflects the new arrangement,
   * then persists: a cross-section move re-homes the item (updateItem) before the global reorder.
   */
  dropItem(event: CdkDragDrop<TemplateItem[]>): void {
    if (!this.canEditStructure()) {
      return;
    }
    const sourceName = this.sectionFromListId(event.previousContainer.id);
    const targetName = this.sectionFromListId(event.container.id);
    if (event.previousContainer === event.container && event.previousIndex === event.currentIndex) {
      return;
    }

    // Work on fresh copies of the grouped view, apply the move, then flatten to the new global order.
    const groups = this.sectionGroups().map((g) => ({ name: g.name, items: [...g.items] }));
    const source = groups.find((g) => g.name === sourceName);
    const target = groups.find((g) => g.name === targetName);
    if (!source || !target) {
      return;
    }
    if (source === target) {
      moveItemInArray(target.items, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(source.items, target.items, event.previousIndex, event.currentIndex);
    }
    const movedItem = target.items[event.currentIndex];
    const orderedItemIds = groups.flatMap((g) => g.items.map((i) => i.id));

    // Optimistic local update so the list doesn't snap back before the request resolves.
    this.applyOptimisticReorder(groups);

    const reorder$ = this.templatesService.reorderItems(this.id(), { orderedItemIds });
    const sectionChanged = sourceName !== targetName;
    if (sectionChanged && movedItem) {
      this.templatesService
        .updateItem(this.id(), movedItem.id, {
          prompt: movedItem.prompt,
          referenceNotes: movedItem.referenceNotes,
          responseType: movedItem.responseType,
          sectionName: targetName,
          isRequired: movedItem.isRequired,
          defaultAssignmentRuleJson: movedItem.defaultAssignmentRuleJson,
          responseConfigJson: movedItem.responseConfigJson,
          riskRating: movedItem.riskRating,
        })
        .pipe(switchMap(() => reorder$))
        .subscribe({ next: () => this.refresh(), error: () => this.refresh() });
    } else {
      reorder$.subscribe({ next: () => this.refresh(), error: () => this.refresh() });
    }
  }

  private applyOptimisticReorder(
    groups: readonly { name: string; items: TemplateItem[] }[],
  ): void {
    const t = this.template();
    if (!t) {
      return;
    }
    const items: TemplateItem[] = [];
    let order = 0;
    for (const group of groups) {
      for (const item of group.items) {
        items.push({ ...item, sectionName: group.name, orderIndex: order++ });
      }
    }
    this.template.set({ ...t, items });
  }

  /**
   * Reorder named sections. Indices are into the full grouped view (which pins the un-sectioned
   * group last and disables dragging it); the un-sectioned group is filtered out before persisting.
   */
  dropSection(event: CdkDragDrop<SectionGroup[]>): void {
    if (!this.canEditStructure() || event.previousIndex === event.currentIndex) {
      return;
    }
    const names = this.sectionGroups().map((g) => g.name);
    moveItemInArray(names, event.previousIndex, event.currentIndex);
    const realNames = names.filter((n) => n !== '');
    if (realNames.length === 0) {
      return;
    }
    this.applyOptimisticSectionReorder(realNames);
    this.templatesService
      .reorderSections(this.id(), { orderedSectionNames: realNames })
      .subscribe({ next: () => this.refresh(), error: () => this.refresh() });
  }

  private applyOptimisticSectionReorder(orderedNames: readonly string[]): void {
    const t = this.template();
    if (!t) {
      return;
    }
    const order = new Map(orderedNames.map((name, index) => [name, index]));
    const sections = t.sections.map((s) => ({
      ...s,
      orderIndex: order.get(s.name) ?? s.orderIndex,
    }));
    this.template.set({ ...t, sections });
  }

  /* ---- Lifecycle ---- */

  publish(): void {
    this.saving.set(true);
    this.templatesService.publish(this.id()).subscribe({
      next: (result) => {
        this.saving.set(false);
        if (result.kind === 'pending') {
          this.notify.info(
            this.i18n.translate('templatesAdmin.notify.publishPending'),
          );
        } else {
          this.applyTemplate(result.template);
          this.notify.success(this.i18n.translate('templatesAdmin.notify.published'));
        }
      },
      error: () => this.saving.set(false),
    });
  }

  newDraft(): void {
    this.saving.set(true);
    this.templatesService.newDraft(this.id()).subscribe({
      next: (t) => {
        this.saving.set(false);
        this.applyTemplate(t);
        this.notify.success(this.i18n.translate('templatesAdmin.notify.newDraftCreated'));
      },
      error: () => this.saving.set(false),
    });
  }

  private refresh(): void {
    this.templatesService.getById(this.id()).subscribe({
      next: (t) => this.applyTemplate(t),
    });
  }
}
