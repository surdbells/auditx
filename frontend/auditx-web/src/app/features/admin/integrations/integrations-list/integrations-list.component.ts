import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatMenuModule } from '@angular/material/menu';
import { MatTableModule } from '@angular/material/table';

import { IntegrationsService } from '../../../../core/services/integrations.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import {
  Integration,
  IntegrationHealthState,
} from '../../../../core/models';
import { humaniseIntegrationType } from '../integration-type-label';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  IntegrationEditorDialogComponent,
  IntegrationEditorDialogData,
  IntegrationEditorResult,
} from '../dialogs/integration-editor-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the integrations console (drives the walkthrough + the About panel). */
const INTEGRATIONS_GUIDE: PageGuide = {
  id: 'integrations-list',
  titleKey: 'integrations.list.title',
  purposeKey: 'integrations.guide.purpose',
  descriptionKey: 'integrations.guide.description',
  actionKeys: [
    'integrations.guide.action.create',
    'integrations.guide.action.test',
    'integrations.guide.action.edit',
    'integrations.guide.action.deactivate',
  ],
  sections: [
    { selector: '[data-guide="create"]', titleKey: 'integrations.guide.section.create.title', bodyKey: 'integrations.guide.section.create.body' },
    { selector: '.integrations__table', titleKey: 'integrations.guide.section.table.title', bodyKey: 'integrations.guide.section.table.body' },
    { selector: '.status-badge', titleKey: 'integrations.guide.section.health.title', bodyKey: 'integrations.guide.section.health.body' },
  ],
  workflowKeys: ['integrations.guide.flow.configure', 'integrations.guide.flow.test', 'integrations.guide.flow.activate', 'integrations.guide.flow.emit', 'integrations.guide.flow.monitor'],
  dependsOnKeys: ['integrations.guide.dep.permissions', 'integrations.guide.dep.endpoints', 'integrations.guide.dep.credentials', 'integrations.guide.dep.events'],
  usedByKeys: ['integrations.guide.use.notifications', 'integrations.guide.use.webhooks', 'integrations.guide.use.siem', 'integrations.guide.use.reports'],
  businessRuleKeys: ['integrations.guide.rule.primary', 'integrations.guide.rule.test', 'integrations.guide.rule.deactivate', 'integrations.guide.rule.health'],
  tipKeys: ['integrations.guide.tip.test', 'integrations.guide.tip.health', 'integrations.guide.tip.primary'],
  permissionKeys: ['integrations.guide.perm.admin', 'integrations.guide.perm.configure', 'integrations.guide.perm.health'],
  faq: [
    { questionKey: 'integrations.guide.faq.manage.q', answerKey: 'integrations.guide.faq.manage.a' },
    { questionKey: 'integrations.guide.faq.health.q', answerKey: 'integrations.guide.faq.health.a' },
  ],
};

@Component({
  selector: 'app-integrations-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    MatMenuModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './integrations-list.component.html',
  styleUrl: './integrations-list.component.scss',
})
export class IntegrationsListComponent {
  private readonly integrationsService = inject(IntegrationsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'name',
    'type',
    'health',
    'active',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly integrations = signal<Integration[]>([]);
  /** integrationId → health state, populated lazily. */
  readonly health = signal<Record<string, IntegrationHealthState>>({});

  readonly humaniseType = humaniseIntegrationType;

  readonly guide = INTEGRATIONS_GUIDE;

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ConfigureIntegrations),
  );

  readonly canViewHealth = computed(() =>
    this.auth.hasPermission(Permissions.ViewIntegrationHealth),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.integrations().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.integrationsService.list().subscribe({
      next: (items) => {
        this.integrations.set(items);
        this.state.set('ready');
        if (this.canViewHealth()) {
          items.forEach((i) => this.loadHealth(i.id));
        }
      },
      error: () => this.state.set('error'),
    });
  }

  private loadHealth(id: string): void {
    this.integrationsService.health(id).subscribe({
      next: (h) =>
        this.health.update((map) => ({ ...map, [id]: h.state })),
      error: () => {
        // Health is best-effort; leave the cell blank on failure.
      },
    });
  }

  healthState(id: string): IntegrationHealthState | null {
    return this.health()[id] ?? null;
  }

  /** Capitalises a health state for display (e.g. `healthy` → `Healthy`). */
  humaniseHealth(state: IntegrationHealthState | null): string {
    return state ? state.charAt(0).toUpperCase() + state.slice(1) : '';
  }

  create(): void {
    const data: IntegrationEditorDialogData = {
      candidates: this.integrations(),
    };
    this.dialog
      .open(IntegrationEditorDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: IntegrationEditorResult) => {
        if (!result || result.mode !== 'create') {
          return;
        }
        this.integrationsService.create(result.body).subscribe({
          next: (created) => {
            this.notify.success(
              this.i18n.translate('integrations.list.created.success', {
                name: created.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }

  edit(integration: Integration): void {
    const data: IntegrationEditorDialogData = {
      integration,
      candidates: this.integrations().filter((i) => i.id !== integration.id),
    };
    this.dialog
      .open(IntegrationEditorDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: IntegrationEditorResult) => {
        if (!result || result.mode !== 'edit') {
          return;
        }
        this.integrationsService.update(result.id, result.body).subscribe({
          next: (updated) => {
            this.notify.success(
              this.i18n.translate('integrations.list.updated.success', {
                name: updated.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }

  deactivate(integration: Integration): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('integrations.list.deactivate.title'),
      message: this.i18n.translate('integrations.list.deactivate.message', {
        name: integration.name,
      }),
      confirmLabel: this.i18n.translate('integrations.actions.deactivate'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.integrationsService.deactivate(integration.id).subscribe({
          next: () => {
            this.notify.success(
              this.i18n.translate('integrations.list.deactivated.success', {
                name: integration.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }

  test(integration: Integration): void {
    this.integrationsService.test(integration.id).subscribe({
      next: (result) => {
        if (result.success) {
          this.notify.success(
            this.i18n.translate('integrations.list.test.success', {
              detail: result.detail,
            }),
          );
        } else {
          this.notify.warning(
            this.i18n.translate('integrations.list.test.failed', {
              detail: result.detail,
            }),
          );
        }
      },
    });
  }
}
