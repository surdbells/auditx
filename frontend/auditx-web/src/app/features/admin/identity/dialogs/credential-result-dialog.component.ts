import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
} from '@angular/material/dialog';

import { IconComponent } from '../../../../core/icons/icon.component';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface CredentialResultDialogData {
  username: string;
  /** The one-time temporary password to reveal. */
  password: string;
}

/**
 * Reveals a just-generated temporary credential once, with copy-to-clipboard for the username and password.
 * The secret is passed in and rendered directly (never via i18n interpolation), so it always shows verbatim.
 */
@Component({
  selector: 'app-credential-result-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, IconComponent, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'identity.credentialResult.title' | t }}</h2>
    <mat-dialog-content>
      <p class="warn">{{ 'identity.credentialResult.onceWarning' | t }}</p>

      <div class="cred">
        <div class="cred__row">
          <span class="cred__label">{{ 'identity.credentialResult.username' | t }}</span>
          <div class="cred__value">
            <code>{{ data.username }}</code>
            <button matIconButton type="button" (click)="copy(data.username, 'username')"
              [attr.aria-label]="'identity.credentialResult.copy' | t">
              <app-icon [name]="copied() === 'username' ? 'check' : 'content_copy'" />
            </button>
          </div>
        </div>
        <div class="cred__row">
          <span class="cred__label">{{ 'identity.credentialResult.password' | t }}</span>
          <div class="cred__value">
            <code>{{ data.password }}</code>
            <button matIconButton type="button" (click)="copy(data.password, 'password')"
              [attr.aria-label]="'identity.credentialResult.copy' | t">
              <app-icon [name]="copied() === 'password' ? 'check' : 'content_copy'" />
            </button>
          </div>
        </div>
      </div>

      @if (copied(); as what) {
        <p class="copied" role="status">{{ 'identity.credentialResult.copied' | t }}</p>
      }
      <p class="note">{{ 'identity.credentialResult.mustChange' | t }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton="filled" type="button" mat-dialog-close>{{ 'common.ok' | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    :host { display: block; min-width: 360px; }
    .warn { color: var(--mat-sys-error, #b3261e); margin: 0 0 12px; }
    .cred { display: flex; flex-direction: column; gap: 10px; }
    .cred__row { display: flex; flex-direction: column; gap: 2px; }
    .cred__label { font-size: 12px; text-transform: uppercase; letter-spacing: .05em; color: var(--mat-sys-on-surface-variant, #59617a); }
    .cred__value { display: flex; align-items: center; gap: 8px; background: var(--mat-sys-surface-container, #f3f4f9);
      border: 1px solid var(--mat-sys-outline-variant, #e4e7f0); border-radius: 8px; padding: 4px 4px 4px 12px; }
    .cred__value code { flex: 1; font-family: "Cascadia Code", Consolas, monospace; word-break: break-all; }
    .copied { color: var(--mat-sys-primary, #15803d); margin: 8px 0 0; font-size: 13px; }
    .note { color: var(--mat-sys-on-surface-variant, #59617a); margin: 12px 0 0; }
  `,
})
export class CredentialResultDialogComponent {
  readonly data = inject<CredentialResultDialogData>(MAT_DIALOG_DATA);
  readonly copied = signal<'username' | 'password' | null>(null);

  copy(text: string, field: 'username' | 'password'): void {
    void navigator.clipboard?.writeText(text);
    this.copied.set(field);
  }
}
