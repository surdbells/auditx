import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { MakerCheckerService } from '../../../../core/services/maker-checker.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { MakerCheckerActionDto } from '../../../../core/models';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import {
  RejectActionDialogComponent,
  RejectActionDialogData,
} from '../dialogs/reject-action-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-maker-checker-queue',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './maker-checker-queue.component.html',
  styleUrl: './maker-checker-queue.component.scss',
})
export class MakerCheckerQueueComponent {
  private readonly service = inject(MakerCheckerService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);

  readonly displayedColumns = [
    'actionType',
    'target',
    'maker',
    'createdAt',
    'actions',
  ];

  readonly actionTypes = [
    { value: '', label: 'All action types' },
    { value: 'role_create', label: 'Role create' },
    { value: 'role_update', label: 'Role update' },
    { value: 'role_archive', label: 'Role archive' },
    { value: 'user_role_grant', label: 'User role grant' },
  ];

  readonly actionTypeFilter = new FormControl('', { nonNullable: true });

  readonly state = signal<ViewState>('loading');
  readonly actions = signal<MakerCheckerActionDto[]>([]);
  readonly processingId = signal<string | null>(null);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.actions().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    const filter = this.actionTypeFilter.value || undefined;
    this.service.pending(filter).subscribe({
      next: (actions) => {
        this.actions.set(actions);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  onFilterChange(): void {
    this.fetch();
  }

  approve(action: MakerCheckerActionDto): void {
    const data: ConfirmDialogData = {
      title: 'Approve change',
      message: `Approve "${this.actionLabel(action)}" raised by ${action.makerName}? This will apply the change immediately.`,
      confirmLabel: 'Approve',
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.processingId.set(action.id);
        this.service.approve(action.id).subscribe({
          next: () => {
            this.processingId.set(null);
            this.notify.success('Change approved.');
            this.removeAction(action.id);
          },
          error: (err: HttpErrorResponse) => {
            this.processingId.set(null);
            if (err.status === 403) {
              // Self-approval guard. The interceptor already toasts the server
              // message; reinforce with a clear, specific hint.
              this.notify.warning(
                'You cannot approve a change you raised. A different authoriser must approve it.',
              );
            }
          },
        });
      });
  }

  reject(action: MakerCheckerActionDto): void {
    const data: RejectActionDialogData = {
      actionLabel: this.actionLabel(action),
    };
    this.dialog
      .open(RejectActionDialogComponent, { data })
      .afterClosed()
      .subscribe((reason: string | undefined) => {
        if (!reason) {
          return;
        }
        this.processingId.set(action.id);
        this.service.reject(action.id, { reason }).subscribe({
          next: () => {
            this.processingId.set(null);
            this.notify.success('Change rejected.');
            this.removeAction(action.id);
          },
          error: (err: HttpErrorResponse) => {
            this.processingId.set(null);
            if (err.status === 403) {
              this.notify.warning(
                'You cannot reject a change you raised.',
              );
            }
          },
        });
      });
  }

  actionLabel(action: MakerCheckerActionDto): string {
    return `${action.actionType} on ${action.targetObjectType}`;
  }

  private removeAction(id: string): void {
    this.actions.update((list) => list.filter((a) => a.id !== id));
  }
}
