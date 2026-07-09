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

import { RisksService } from '../../../core/services/risks.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  RISK_BANDS,
  RISK_STATUSES,
  Risk,
  RiskBand,
  RiskListItem,
  RiskStatus,
} from '../../../core/models';
import {
  RiskEditorDialogComponent,
  RiskEditorDialogData,
  RiskEditorResult,
} from '../dialogs/risk-editor-dialog.component';
import {
  TransitionReasonDialogComponent,
  TransitionReasonDialogData,
  TransitionReasonResult,
} from '../../audits/dialogs/transition-reason-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** The enterprise risk register (P1-A): filterable list + register/edit/transition/delete. */
@Component({
  selector: 'app-risk-register',
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
  templateUrl: './risk-register.component.html',
  styleUrl: './risk-register.component.scss',
})
export class RiskRegisterComponent {
  private readonly service = inject(RisksService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);
  readonly userLookup = inject(UserLookupService);

  readonly displayedColumns = ['title', 'category', 'owner', 'rating', 'status', 'target', 'actions'];
  readonly statuses = RISK_STATUSES;
  readonly bands = RISK_BANDS;

  readonly filters = this.fb.nonNullable.group({
    status: '' as RiskStatus | '',
    band: '' as RiskBand | '',
    includeClosed: false,
    search: '',
  });

  readonly state = signal<ViewState>('loading');
  readonly risks = signal<RiskListItem[]>([]);

  readonly canManage = computed(() => this.auth.hasPermission(Permissions.ManageRisk));
  readonly isEmpty = computed(() => this.state() === 'ready' && this.risks().length === 0);

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
        status: f.status || undefined,
        band: f.band || undefined,
        includeClosed: f.includeClosed,
        search: f.search.trim() || undefined,
        limit: 100,
      })
      .subscribe({
        next: (page) => {
          this.risks.set(page.items);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  ownerName(id: string): string {
    return this.userLookup.displayName(id);
  }

  create(): void {
    const data: RiskEditorDialogData = {};
    this.dialog
      .open(RiskEditorDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: RiskEditorResult) => {
        if (result?.mode !== 'register') {
          return;
        }
        this.service.register(result.body).subscribe({
          next: (r) => {
            this.notify.success(this.i18n.translate('risk.notify.registered', { title: r.title }));
            this.fetch();
          },
        });
      });
  }

  /** Loads the full risk (the list row lacks version + residual/treatment), then opens the editor. */
  edit(row: RiskListItem): void {
    this.service.getById(row.id).subscribe({
      next: (risk) => this.openEditor(risk),
    });
  }

  private openEditor(risk: Risk): void {
    const data: RiskEditorDialogData = { risk };
    this.dialog
      .open(RiskEditorDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: RiskEditorResult) => {
        if (result?.mode !== 'update') {
          return;
        }
        this.service.update(result.id, result.body).subscribe({
          next: (r) => {
            this.notify.success(this.i18n.translate('risk.notify.updated', { title: r.title }));
            this.fetch();
          },
        });
      });
  }

  setStatus(row: RiskListItem, status: RiskStatus): void {
    this.service.getById(row.id).subscribe({
      next: (risk) => {
        if (status === 'closed') {
          this.closeWithRationale(risk);
        } else {
          this.transition(risk, status, null);
        }
      },
    });
  }

  private closeWithRationale(risk: Risk): void {
    const data: TransitionReasonDialogData = {
      title: this.i18n.translate('risk.close.title'),
      message: this.i18n.translate('risk.close.message', { title: risk.title }),
      reasonRequired: true,
      confirmLabel: this.i18n.translate('risk.close.confirm'),
    };
    this.dialog
      .open(TransitionReasonDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: TransitionReasonResult) => {
        if (result) {
          this.transition(risk, 'closed', result.reason);
        }
      });
  }

  private transition(risk: Risk, status: RiskStatus, rationale: string | null): void {
    this.service.transition(risk.id, { status, rationale, version: risk.version }).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('risk.notify.statusChanged'));
        this.fetch();
      },
    });
  }

  remove(row: RiskListItem): void {
    this.service.getById(row.id).subscribe({
      next: (risk) =>
        this.service.delete(risk.id, risk.version).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('risk.notify.deleted'));
            this.fetch();
          },
        }),
    });
  }
}
