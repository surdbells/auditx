import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';

import { ReportsService } from '../../../core/services/reports.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { ReportTemplate } from '../../../core/models';
import { ReportTemplateDialogComponent } from '../dialogs/report-template-dialog.component';
import {
  ActivateTemplateDialogComponent,
  ActivateTemplateDialogData,
  ActivateTemplateDialogResult,
} from '../dialogs/activate-template-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-report-templates',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './report-templates.component.html',
  styleUrl: './report-templates.component.scss',
})
export class ReportTemplatesComponent {
  private readonly service = inject(ReportsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  readonly displayedColumns = [
    'name',
    'version',
    'state',
    'createdAt',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly templates = signal<ReportTemplate[]>([]);

  readonly canConfigure = computed(() =>
    this.auth.hasPermission(Permissions.ConfigureReports),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.templates().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.listTemplates().subscribe({
      next: (templates) => {
        this.templates.set(templates);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  create(): void {
    this.dialog
      .open(ReportTemplateDialogComponent, { width: '560px' })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }
        this.service.createTemplate(result).subscribe({
          next: (saved) => {
            this.notify.success(`Template "${saved.name}" created.`);
            this.fetch();
          },
          error: () => this.notify.error('We could not create the template.'),
        });
      });
  }

  activate(template: ReportTemplate): void {
    const data: ActivateTemplateDialogData = {
      templateName: template.name,
      versionNumber: template.versionNumber,
    };
    this.dialog
      .open(ActivateTemplateDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: ActivateTemplateDialogResult) => {
        if (!result) {
          return;
        }
        this.service
          .activateTemplate(template.id, { reason: result.reason })
          .subscribe({
            next: (saved) => {
              this.notify.success(`Template "${saved.name}" activated.`);
              this.fetch();
            },
            error: () =>
              this.notify.error('We could not activate the template.'),
          });
      });
  }
}
