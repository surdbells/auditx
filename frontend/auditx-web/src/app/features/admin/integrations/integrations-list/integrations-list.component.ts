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
import { MatIconModule } from '@angular/material/icon';
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

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-integrations-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './integrations-list.component.html',
  styleUrl: './integrations-list.component.scss',
})
export class IntegrationsListComponent {
  private readonly integrationsService = inject(IntegrationsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

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
            this.notify.success(`Integration "${created.name}" created.`);
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
            this.notify.success(`Integration "${updated.name}" updated.`);
            this.fetch();
          },
        });
      });
  }

  deactivate(integration: Integration): void {
    const data: ConfirmDialogData = {
      title: 'Deactivate integration',
      message: `Deactivate "${integration.name}"? It will stop handling traffic.`,
      confirmLabel: 'Deactivate',
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
            this.notify.success(`"${integration.name}" deactivated.`);
            this.fetch();
          },
        });
      });
  }

  test(integration: Integration): void {
    this.integrationsService.test(integration.id).subscribe({
      next: (result) => {
        if (result.success) {
          this.notify.success(`Test succeeded: ${result.detail}`);
        } else {
          this.notify.warning(`Test failed: ${result.detail}`);
        }
      },
    });
  }
}
