import { ChangeDetectionStrategy, Component, Signal, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { IconComponent } from '../../../core/icons/icon.component';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface IdleWarningDialogData {
  /** Seconds remaining before automatic sign-out (ticked by IdleTimeoutService). */
  remainingSeconds: Signal<number>;
}

/** Result: true = stay signed in; false = sign out now. (Closed with no result by the service on expiry.) */
export type IdleWarningResult = boolean;

/**
 * The session-inactivity warning: shown by {@link IdleTimeoutService} after the configured idle period,
 * counting down to automatic sign-out. Deliberately modal with no backdrop-close — the user must choose
 * (or be signed out when the countdown ends).
 */
@Component({
  selector: 'app-idle-warning-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, IconComponent, TranslatePipe],
  template: `
    <h2 mat-dialog-title class="idle__title">
      <app-icon name="schedule" />
      {{ 'idle.title' | t }}
    </h2>
    <mat-dialog-content>
      <p>{{ 'idle.body' | t }}</p>
      <!-- role="timer" has an implicit aria-live of "off" — deliberately NOT polite, or a screen reader
           would announce every 1-second tick and drown the dialog body/buttons. The dialog itself is
           announced on open, which is the actionable moment. -->
      <p class="idle__countdown" role="timer">
        {{ 'idle.countdown' | t: { seconds: data.remainingSeconds() } }}
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="signOut()">
        {{ 'idle.signOut' | t }}
      </button>
      <button matButton="filled" type="button" (click)="stay()" cdkFocusInitial>
        {{ 'idle.stay' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .idle__title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .idle__countdown {
      font: var(--mat-sys-title-medium);
      color: var(--mat-sys-error);
    }
  `,
})
export class IdleWarningDialogComponent {
  readonly data = inject<IdleWarningDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<IdleWarningDialogComponent, IdleWarningResult>>(MatDialogRef);

  stay(): void {
    this.dialogRef.close(true);
  }

  signOut(): void {
    this.dialogRef.close(false);
  }
}
