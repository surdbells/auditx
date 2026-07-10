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
import { MatIconModule } from '@angular/material/icon';
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
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

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
    MatIconModule,
    MatMenuModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
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

  readonly canManage = computed(() => this.auth.hasPermission(Permissions.ManageControls));
  readonly isEmpty = computed(() => this.state() === 'ready' && this.controls().length === 0);

  constructor() {
    this.userLookup.ensureLoaded();
    this.fetch();
    this.filters.valueChanges.pipe(debounceTime(250), takeUntilDestroyed()).subscribe(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    const f = this.filters.getRawValue();
    this.service
      .list({
        type: f.type || undefined,
        effectiveness: f.effectiveness || undefined,
        includeRetired: f.includeRetired,
        search: f.search.trim() || undefined,
        limit: 100,
      })
      .subscribe({
        next: (page) => {
          this.controls.set(page.items);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
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
            this.fetch();
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
            this.fetch();
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
            this.fetch();
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
            this.fetch();
          },
        }),
    });
  }
}
