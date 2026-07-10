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
import { MatMenuModule } from '@angular/material/menu';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ReportSchedulesService } from '../../../core/services/report-schedules.service';
import { UsersService } from '../../../core/services/users.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { ReportSchedule, UserDto } from '../../../core/models';
import { humanise } from '../humanise';
import {
  ReportScheduleDialogComponent,
  ReportScheduleDialogData,
  ReportScheduleFormResult,
} from '../dialogs/report-schedule-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Admin console for recurring report schedules (D3-C). */
@Component({
  selector: 'app-report-schedules',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './report-schedules.component.html',
  styleUrl: './report-schedules.component.scss',
})
export class ReportSchedulesComponent {
  private readonly service = inject(ReportSchedulesService);
  private readonly usersService = inject(UsersService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = ['name', 'kind', 'cadence', 'recipients', 'nextRun', 'status', 'actions'];

  readonly state = signal<ViewState>('loading');
  readonly schedules = signal<ReportSchedule[]>([]);
  private readonly users = signal<UserDto[]>([]);

  readonly humanise = humanise;

  readonly canManage = computed(() => this.auth.hasPermission(Permissions.ScheduleReports));

  readonly isEmpty = computed(() => this.state() === 'ready' && this.schedules().length === 0);

  constructor() {
    this.fetch();
    // Active users for the recipient picker (fire-and-forget; the dialog still works with ad-hoc emails only).
    this.usersService.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => this.users.set(page.items),
      error: () => undefined,
    });
  }

  fetch(): void {
    this.state.set('loading');
    this.service.list().subscribe({
      next: (rows) => {
        this.schedules.set(rows);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  recipientCount(s: ReportSchedule): number {
    return s.recipientUserIds.length + s.recipientEmails.length;
  }

  create(): void {
    const data: ReportScheduleDialogData = { users: this.users() };
    this.dialog
      .open(ReportScheduleDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: ReportScheduleFormResult) => {
        if (!result) {
          return;
        }
        this.service
          .create({
            name: result.name,
            kind: result.kind,
            cadence: result.cadence,
            recipientUserIds: result.recipientUserIds,
            recipientEmails: result.recipientEmails,
          })
          .subscribe({
            next: () => {
              this.notify.success(this.i18n.translate('reportSchedules.notify.created'));
              this.fetch();
            },
            error: () => this.notify.error(this.i18n.translate('reportSchedules.notify.saveError')),
          });
      });
  }

  edit(schedule: ReportSchedule): void {
    const data: ReportScheduleDialogData = { schedule, users: this.users() };
    this.dialog
      .open(ReportScheduleDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: ReportScheduleFormResult) => {
        if (!result) {
          return;
        }
        this.service
          .update(schedule.id, {
            name: result.name,
            cadence: result.cadence,
            recipientUserIds: result.recipientUserIds,
            recipientEmails: result.recipientEmails,
            isActive: result.isActive,
            version: schedule.version,
          })
          .subscribe({
            next: () => {
              this.notify.success(this.i18n.translate('reportSchedules.notify.updated'));
              this.fetch();
            },
            error: () => this.notify.error(this.i18n.translate('reportSchedules.notify.saveError')),
          });
      });
  }

  remove(schedule: ReportSchedule): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('reportSchedules.delete.title'),
      message: this.i18n.translate('reportSchedules.delete.message', { name: schedule.name }),
      confirmLabel: this.i18n.translate('reportSchedules.delete.confirm'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed?: boolean) => {
        if (!confirmed) {
          return;
        }
        this.service.delete(schedule.id, schedule.version).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('reportSchedules.notify.deleted'));
            this.fetch();
          },
          error: () => this.notify.error(this.i18n.translate('reportSchedules.notify.deleteError')),
        });
      });
  }
}
