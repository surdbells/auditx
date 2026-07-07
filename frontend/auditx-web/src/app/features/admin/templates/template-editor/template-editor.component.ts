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
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';

import { TemplatesService } from '../../../../core/services/templates.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
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

type ViewState = 'loading' | 'ready' | 'error';

/** A section grouping with its ordered items, for rendering. */
interface SectionGroup {
  name: string;
  items: TemplateItem[];
}

@Component({
  selector: 'app-template-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatChipsModule,
    MatTooltipModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
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
            this.notify.success('Template created.');
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
            this.notify.success('Template updated.');
          },
          error: () => this.saving.set(false),
        });
    }
  }

  /* ---- Sections ---- */

  addSection(): void {
    const data: SectionDialogData = {
      title: 'Add section',
      confirmLabel: 'Add',
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
            this.notify.success('Section added.');
          },
        });
      });
  }

  renameSection(currentName: string): void {
    const data: SectionDialogData = {
      title: 'Rename section',
      confirmLabel: 'Rename',
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
              this.notify.success('Section renamed.');
              this.refresh();
            },
          });
      });
  }

  removeSection(name: string): void {
    const data: ConfirmDialogData = {
      title: 'Remove section',
      message: `Remove the "${name}" section? Its items will become unsectioned.`,
      confirmLabel: 'Remove',
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
            this.notify.success('Section removed.');
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
            this.notify.success('Item added.');
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
            this.notify.success('Item updated.');
          },
        });
      });
  }

  removeItem(item: TemplateItem): void {
    const data: ConfirmDialogData = {
      title: 'Remove item',
      message: 'Remove this item from the template?',
      confirmLabel: 'Remove',
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
            this.notify.success('Item removed.');
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

  /* ---- Lifecycle ---- */

  publish(): void {
    this.saving.set(true);
    this.templatesService.publish(this.id()).subscribe({
      next: (result) => {
        this.saving.set(false);
        if (result.kind === 'pending') {
          this.notify.info(
            'Sent for approval. The template will publish once a second authoriser approves it.',
          );
        } else {
          this.applyTemplate(result.template);
          this.notify.success('Template published.');
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
        this.notify.success('New draft version created.');
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
