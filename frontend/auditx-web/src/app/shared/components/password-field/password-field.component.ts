import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  input,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ValidationErrors,
  Validator,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { IconComponent } from '../../../core/icons/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { PasswordPolicy } from '../../../core/models';

/** Fallback policy when the institution policy has not been supplied (mirrors the server defaults). */
export const DEFAULT_PASSWORD_POLICY: Pick<
  PasswordPolicy,
  'minLength' | 'requireUppercase' | 'requireLowercase' | 'requireDigit' | 'requireSymbol'
> = {
  minLength: 12,
  requireUppercase: true,
  requireLowercase: true,
  requireDigit: true,
  requireSymbol: true,
};

interface Requirement {
  readonly key: string;
  readonly met: boolean;
}

/**
 * A reusable password input: a reveal (show/hide) toggle, an optional confirm field, and a live requirements
 * checklist derived from the institution password policy — so the user sees, as they type, exactly which rules
 * are still unmet instead of discovering a rejection only after submitting. Implements ControlValueAccessor +
 * Validator, so it drops into a reactive form with `formControlName` and participates in form validity.
 */
@Component({
  selector: 'app-password-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatFormFieldModule, MatInputModule, MatButtonModule, IconComponent, TranslatePipe],
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => PasswordFieldComponent), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => PasswordFieldComponent), multi: true },
  ],
  template: `
    <mat-form-field appearance="outline" class="pf-field">
      <mat-label>{{ label() || ('identity.createUser.password' | t) }}</mat-label>
      <input
        matInput
        [type]="reveal() ? 'text' : 'password'"
        autocomplete="new-password"
        [value]="password()"
        (input)="onInput($event)"
        (blur)="onTouched()"
      />
      <button
        matSuffix
        matIconButton
        type="button"
        tabindex="-1"
        (click)="reveal.set(!reveal())"
        [attr.aria-label]="(reveal() ? 'auth.login.hidePassword' : 'auth.login.showPassword') | t"
      >
        <app-icon [name]="reveal() ? 'visibility_off' : 'visibility'" />
      </button>
    </mat-form-field>

    @if (requirements().length) {
      <ul class="pf-reqs" [attr.aria-label]="'identity.password.requirements' | t">
        @for (r of requirements(); track r.key) {
          <li class="pf-req" [class.pf-req--met]="r.met">
            <app-icon [name]="r.met ? 'check_circle' : 'cancel'" aria-hidden="true" />
            <span>{{ r.key | t: reqParams() }}</span>
          </li>
        }
      </ul>
    }

    @if (showConfirm()) {
      <mat-form-field appearance="outline" class="pf-field">
        <mat-label>{{ 'identity.password.confirm' | t }}</mat-label>
        <input
          matInput
          [type]="revealConfirm() ? 'text' : 'password'"
          autocomplete="new-password"
          [value]="confirm()"
          (input)="onConfirmInput($event)"
          (blur)="onTouched()"
        />
        <button
          matSuffix
          matIconButton
          type="button"
          tabindex="-1"
          (click)="revealConfirm.set(!revealConfirm())"
          [attr.aria-label]="(revealConfirm() ? 'auth.login.hidePassword' : 'auth.login.showPassword') | t"
        >
          <app-icon [name]="revealConfirm() ? 'visibility_off' : 'visibility'" />
        </button>
      </mat-form-field>
      @if (confirm() && confirm() !== password()) {
        <p class="pf-error">{{ 'identity.password.mismatch' | t }}</p>
      }
    }
  `,
  styles: `
    :host { display: block; }
    .pf-field { width: 100%; }
    .pf-reqs {
      list-style: none;
      margin: -4px 0 12px;
      padding: 0;
      display: grid;
      gap: 4px;
    }
    .pf-req {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 0.8125rem;
      color: var(--mat-sys-on-surface-variant, #64748b);
    }
    .pf-req app-icon { width: 16px; height: 16px; flex: 0 0 16px; color: currentColor; }
    .pf-req--met { color: var(--ax-success, #16a34a); }
    .pf-error {
      margin: -4px 0 8px;
      font-size: 0.8125rem;
      color: var(--mat-sys-error, #dc2626);
    }
  `,
})
export class PasswordFieldComponent implements ControlValueAccessor, Validator {
  readonly label = input<string>('');
  /** Institution password policy; falls back to the server defaults when not supplied. */
  readonly policy = input<PasswordPolicy | null>(null);
  readonly showConfirm = input<boolean>(true);

  readonly password = signal('');
  readonly confirm = signal('');
  readonly reveal = signal(false);
  readonly revealConfirm = signal(false);

  private onChange: (value: string) => void = () => {};
  onTouched: () => void = () => {};
  private validatorOnChange: () => void = () => {};

  private readonly effectivePolicy = computed(() => this.policy() ?? DEFAULT_PASSWORD_POLICY);

  readonly reqParams = computed(() => ({ min: this.effectivePolicy().minLength }));

  /** The live requirement rows for the enabled policy rules, each flagged met/unmet against the current value. */
  readonly requirements = computed<Requirement[]>(() => {
    const p = this.effectivePolicy();
    const v = this.password();
    const rows: Requirement[] = [
      { key: 'identity.password.req.minLength', met: v.length >= p.minLength },
    ];
    if (p.requireUppercase) {
      rows.push({ key: 'identity.password.req.uppercase', met: /[A-Z]/.test(v) });
    }
    if (p.requireLowercase) {
      rows.push({ key: 'identity.password.req.lowercase', met: /[a-z]/.test(v) });
    }
    if (p.requireDigit) {
      rows.push({ key: 'identity.password.req.digit', met: /[0-9]/.test(v) });
    }
    if (p.requireSymbol) {
      rows.push({ key: 'identity.password.req.symbol', met: /[^A-Za-z0-9]/.test(v) });
    }
    return rows;
  });

  onInput(event: Event): void {
    this.password.set((event.target as HTMLInputElement).value);
    this.onChange(this.password());
    this.validatorOnChange();
  }

  onConfirmInput(event: Event): void {
    this.confirm.set((event.target as HTMLInputElement).value);
    this.validatorOnChange();
  }

  // ControlValueAccessor
  writeValue(value: string | null): void {
    this.password.set(value ?? '');
  }
  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  // Validator — invalid until every enabled rule is met (and the confirm matches, when shown).
  validate(_control: AbstractControl): ValidationErrors | null {
    const errors: ValidationErrors = {};
    if (this.requirements().some((r) => !r.met)) {
      errors['passwordPolicy'] = true;
    }
    if (this.showConfirm() && this.confirm() !== this.password()) {
      errors['passwordMismatch'] = true;
    }
    return Object.keys(errors).length ? errors : null;
  }
  registerOnValidatorChange(fn: () => void): void {
    this.validatorOnChange = fn;
  }
}
