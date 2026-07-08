import {
  ChangeDetectorRef,
  Pipe,
  PipeTransform,
  effect,
  inject,
} from '@angular/core';

import { TranslationService } from './translation.service';

/**
 * Resolves a translation key: `{{ 'auth.login.title' | t }}` or with params
 * `{{ 'greeting' | t: { name: user() } }}`.
 *
 * Impure so it recomputes when the language changes; an effect tracks the language
 * signal and marks the host view for check, so `| t` bindings update on toggle even
 * in OnPush / zoneless views that don't otherwise read the signal.
 */
@Pipe({ name: 't', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly translation = inject(TranslationService);
  private readonly cdr = inject(ChangeDetectorRef);

  constructor() {
    effect(() => {
      this.translation.lang();
      this.cdr.markForCheck();
    });
  }

  transform(
    key: string,
    params?: Record<string, string | number | null | undefined>,
  ): string {
    return this.translation.translate(key, params);
  }
}
