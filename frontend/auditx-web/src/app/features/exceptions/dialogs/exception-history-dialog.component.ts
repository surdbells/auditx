import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';

import { ExceptionsService } from '../../../core/services/exceptions.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { ExceptionHistoryEntry } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

export interface ExceptionHistoryDialogData {
  exceptionId: string;
  title: string;
}

type LoadState = 'loading' | 'ready' | 'error';

/** Read-only timeline of the lifecycle events recorded against an exception. */
@Component({
  selector: 'app-exception-history-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatDialogModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'exceptions.dialog.historyTitle' | t }}</h2>
    <mat-dialog-content>
      <p class="prompt">{{ data.title }}</p>
      @switch (state()) {
        @case ('loading') {
          <p class="muted">{{ 'exceptions.history.loading' | t }}</p>
        }
        @case ('error') {
          <p class="muted">{{ 'exceptions.history.loadError' | t }}</p>
        }
        @case ('ready') {
          @if (!entries().length) {
            <p class="muted">{{ 'exceptions.history.empty' | t }}</p>
          } @else {
            <ol class="timeline">
              @for (e of entries(); track e.id) {
                <li class="timeline__entry">
                  <div class="timeline__dot" aria-hidden="true"></div>
                  <div class="timeline__body">
                    <span class="timeline__event">{{ label(e.eventType) }}</span>
                    <span class="muted timeline__meta">
                      {{ nameOf(e.actorUserId) }} ·
                      {{ e.occurredAtUtc | date: 'medium' }}
                    </span>
                  </div>
                </li>
              }
            </ol>
          }
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="close()">{{ 'exceptions.action.closeDialog' | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .prompt {
      margin: 0 0 1rem;
      font-weight: 500;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .timeline {
      list-style: none;
      margin: 0;
      padding: 0;
      min-width: 420px;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .timeline__entry {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
    }
    .timeline__dot {
      margin-top: 0.4rem;
      width: 0.6rem;
      height: 0.6rem;
      border-radius: 50%;
      background: var(--mat-sys-primary);
      flex: 0 0 auto;
    }
    .timeline__body {
      display: flex;
      flex-direction: column;
    }
    .timeline__event {
      font-weight: 500;
      text-transform: capitalize;
    }
    .timeline__meta {
      font: var(--mat-sys-label-small);
    }
    @media (max-width: 520px) {
      .timeline {
        min-width: auto;
      }
    }
  `,
})
export class ExceptionHistoryDialogComponent {
  readonly data = inject<ExceptionHistoryDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ExceptionHistoryDialogComponent>>(MatDialogRef);
  private readonly service = inject(ExceptionsService);
  private readonly i18n = inject(TranslationService);
  private readonly userLookup = inject(UserLookupService);

  readonly state = signal<LoadState>('loading');
  readonly entries = signal<ExceptionHistoryEntry[]>([]);

  constructor() {
    this.service.getHistory(this.data.exceptionId).subscribe({
      next: (entries) => {
        this.entries.set(entries);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  label(eventType: string): string {
    return eventType.replace(/_/g, ' ');
  }

  nameOf(userId: string | null | undefined): string {
    // Automated/system lifecycle events carry no actor id.
    if (!userId) {
      return this.i18n.translate('exceptions.history.system');
    }
    const name = this.userLookup.displayName(userId);
    // Never surface a raw id: an unresolved actor (e.g. a deleted user) reads as "System".
    return name === userId ? this.i18n.translate('exceptions.history.system') : name;
  }

  close(): void {
    this.dialogRef.close();
  }
}
