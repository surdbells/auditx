import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { ControlsService } from '../../../core/services/controls.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  CONTROL_EFFECTIVENESS,
  CONTROL_TYPES,
  Control,
  ControlEffectiveness,
  ControlListItem,
  ControlType,
} from '../../../core/models';
import {
  ControlEditorDialogComponent,
  ControlEditorDialogData,
  ControlEditorResult,
} from '../dialogs/control-editor-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Contextual page guide for the controls register (drives the walkthrough + the About panel). */
const CONTROLS_GUIDE: PageGuide = {
  id: 'controls-register',
  titleKey: 'control.list.title',
  purposeKey: 'controls.guide.purpose',
  descriptionKey: 'controls.guide.description',
  actionKeys: [
    'controls.guide.action.register',
    'controls.guide.action.filter',
    'controls.guide.action.test',
    'controls.guide.action.retire',
  ],
  sections: [
    { selector: '[data-guide="register"]', titleKey: 'controls.guide.section.register.title', bodyKey: 'controls.guide.section.register.body' },
    { selector: '.control-list__filters-card', titleKey: 'controls.guide.section.filters.title', bodyKey: 'controls.guide.section.filters.body' },
    { selector: '.control-list__table', titleKey: 'controls.guide.section.table.title', bodyKey: 'controls.guide.section.table.body' },
  ],
  workflowKeys: ['controls.guide.flow.register', 'controls.guide.flow.classify', 'controls.guide.flow.test', 'controls.guide.flow.link', 'controls.guide.flow.report'],
  dependsOnKeys: ['controls.guide.dep.users', 'controls.guide.dep.regulations', 'controls.guide.dep.universe'],
  usedByKeys: ['controls.guide.use.findings', 'controls.guide.use.compliance', 'controls.guide.use.reports'],
  businessRuleKeys: ['controls.guide.rule.testing', 'controls.guide.rule.retire', 'controls.guide.rule.code', 'controls.guide.rule.type'],
  tipKeys: ['controls.guide.tip.filter', 'controls.guide.tip.retire', 'controls.guide.tip.compliance'],
  permissionKeys: ['controls.guide.perm.manage', 'controls.guide.perm.view'],
  faq: [
    { questionKey: 'controls.guide.faq.register.q', answerKey: 'controls.guide.faq.register.a' },
    { questionKey: 'controls.guide.faq.effectiveness.q', answerKey: 'controls.guide.faq.effectiveness.a' },
  ],
};

/** The internal-controls register (P1-B): filterable list + register/edit/retire/delete. */
@Component({
  selector: 'app-control-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    IconComponent,
    MatMenuModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './control-register.component.html',
  styleUrl: './control-register.component.scss',
})
export class ControlRegisterComponent {
  private readonly service = inject(ControlsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);
  readonly userLookup = inject(UserLookupService);

  readonly displayedColumns = ['code', 'title', 'owner', 'type', 'frequency', 'effectiveness', 'status', 'actions'];
  readonly types = CONTROL_TYPES;
  readonly effectivenessOptions = CONTROL_EFFECTIVENESS;

  readonly filters = this.fb.nonNullable.group({
    type: '' as ControlType | '',
    effectiveness: '' as ControlEffectiveness | '',
    includeRetired: true,
    search: '',
  });

  readonly state = signal<ViewState>('loading');
  readonly controls = signal<ControlListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly guide = CONTROLS_GUIDE;

  readonly canManage = computed(() => this.auth.hasPermission(Permissions.ManageControls));
  readonly isEmpty = computed(() => this.state() === 'ready' && this.controls().length === 0);

  constructor() {
    this.userLookup.ensureLoaded();
    this.fetchPage(1);
    this.filters.valueChanges.pipe(debounceTime(250), takeUntilDestroyed()).subscribe(() => this.fetchPage(1));
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    const f = this.filters.getRawValue();
    this.service
      .list({
        type: f.type || undefined,
        effectiveness: f.effectiveness || undefined,
        includeRetired: f.includeRetired,
        search: f.search.trim() || undefined,
        page,
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.controls.set(result.items);
          this.total.set(result.total);
          this.page.set(result.page);
          this.state.set('ready');
          this.loading.set(false);
        },
        error: () => {
          if (this.state() === 'loading') {
            this.state.set('error');
          }
          this.loading.set(false);
        },
      });
  }

  onPageChange(page: number): void {
    this.fetchPage(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.fetchPage(1);
  }

  ownerName(id: string): string {
    return this.userLookup.displayName(id);
  }

  create(): void {
    const data: ControlEditorDialogData = {};
    this.dialog
      .open(ControlEditorDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: ControlEditorResult) => {
        if (result?.mode !== 'register') {
          return;
        }
        this.service.register(result.body).subscribe({
          next: (c) => {
            this.notify.success(this.i18n.translate('control.notify.registered', { code: c.code }));
            this.fetchPage(this.page());
          },
        });
      });
  }

  /** Loads the full control (the list row lacks version + description/entity), then opens the editor. */
  edit(row: ControlListItem): void {
    this.service.getById(row.id).subscribe({
      next: (control) => this.openEditor(control),
    });
  }

  private openEditor(control: Control): void {
    const data: ControlEditorDialogData = { control };
    this.dialog
      .open(ControlEditorDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: ControlEditorResult) => {
        if (result?.mode !== 'update') {
          return;
        }
        this.service.update(result.id, result.body).subscribe({
          next: (c) => {
            this.notify.success(this.i18n.translate('control.notify.updated', { code: c.code }));
            this.fetchPage(this.page());
          },
        });
      });
  }

  toggleActive(row: ControlListItem): void {
    this.service.getById(row.id).subscribe({
      next: (control) =>
        this.service.setStatus(control.id, { isActive: !control.isActive, version: control.version }).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('control.notify.statusChanged'));
            this.fetchPage(this.page());
          },
        }),
    });
  }

  remove(row: ControlListItem): void {
    this.service.getById(row.id).subscribe({
      next: (control) =>
        this.service.delete(control.id, control.version).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('control.notify.deleted'));
            this.fetchPage(this.page());
          },
        }),
    });
  }
}
