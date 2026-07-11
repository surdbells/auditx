import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { MatDialog, MatDialogRef, MatDialogState } from '@angular/material/dialog';
import { Router } from '@angular/router';

import { AuthService } from './auth.service';
import { BrandingService } from './branding.service';
import { NotificationService } from './notification.service';
import { TranslationService } from '../i18n/translation.service';
import {
  IdleWarningDialogComponent,
  IdleWarningDialogData,
  IdleWarningResult,
} from '../../shared/components/idle-warning/idle-warning-dialog.component';

/** DOM events that count as user activity (passive listeners — never block scrolling/input). */
const ACTIVITY_EVENTS = ['mousemove', 'mousedown', 'keydown', 'wheel', 'touchstart', 'scroll'] as const;

/** How often the idle state is evaluated. */
const TICK_MS = 1_000;

/** Cross-tab activity beacon: activity in ANY tab keeps every tab's watchdog from firing. */
const ACTIVITY_STORAGE_KEY = 'auditx.idle.lastActivity';

/** How often (at most) the activity beacon is written to localStorage. */
const BEACON_MS = 5_000;

/**
 * Session-inactivity (idle timeout) watchdog. After the admin-configured minutes of inactivity
 * (`BankSettings.IdleTimeoutMinutes`, 0 = disabled) it opens a modal warning that counts down
 * `IdleWarningSeconds`; unless the user chooses to stay signed in, they are signed out automatically.
 *
 * Started once from the authenticated shell. Config is read live from {@link BrandingService} signals,
 * so an admin save takes effect without a reload. Only armed while a session exists.
 */
@Injectable({ providedIn: 'root' })
export class IdleTimeoutService {
  private readonly auth = inject(AuthService);
  private readonly branding = inject(BrandingService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  private started = false;
  private lastActivity = Date.now();
  private intervalId: ReturnType<typeof setInterval> | null = null;

  /** The open warning dialog, if any; also acts as the "warning phase" flag. */
  private warningRef: MatDialogRef<IdleWarningDialogComponent, IdleWarningResult> | null = null;
  /** When the warning opened (countdown anchor). */
  private warningStartedAt = 0;
  /** Remaining countdown seconds, ticked for the dialog. */
  private readonly remainingSeconds = signal(0);

  /** Idempotently starts the watchdog (called from the authenticated shell). */
  start(): void {
    if (this.started) {
      return;
    }
    this.started = true;

    let lastBeacon = 0;
    const onActivity = () => {
      // While the warning is up, background activity must not silently dismiss it — the
      // user has to explicitly choose to stay signed in.
      if (!this.warningRef) {
        this.lastActivity = Date.now();
        // Throttled cross-tab beacon so activity here also disarms the other tabs' watchdogs.
        if (this.lastActivity - lastBeacon >= BEACON_MS) {
          lastBeacon = this.lastActivity;
          this.writeBeacon(this.lastActivity);
        }
      }
    };
    for (const type of ACTIVITY_EVENTS) {
      this.document.addEventListener(type, onActivity, { passive: true, capture: true });
    }

    this.intervalId = setInterval(() => this.tick(), TICK_MS);

    // Root-service lifetime = app lifetime; this teardown matters for test hosts.
    this.destroyRef.onDestroy(() => {
      if (this.intervalId) {
        clearInterval(this.intervalId);
      }
      for (const type of ACTIVITY_EVENTS) {
        this.document.removeEventListener(type, onActivity, { capture: true });
      }
    });
  }

  private tick(): void {
    // Disarmed: no session (login screen / already signed out) or the policy is off.
    const minutes = this.branding.idleTimeoutMinutes();
    if (!this.auth.session() || minutes <= 0) {
      this.lastActivity = Date.now();
      this.closeWarning();
      return;
    }

    const now = Date.now();

    if (this.warningRef) {
      // If the user already chose (dialog is mid-close animation), let afterClosed handle the outcome —
      // an expiry tick landing in that window must NOT override a "Stay signed in" click.
      if (this.warningRef.getState() !== MatDialogState.OPEN) {
        return;
      }

      const elapsed = Math.floor((now - this.warningStartedAt) / 1000);
      const remaining = Math.max(0, this.branding.idleWarningSeconds() - elapsed);
      this.remainingSeconds.set(remaining);
      if (remaining <= 0) {
        this.closeWarning();
        this.signOut();
      }
      return;
    }

    // Activity in ANY tab counts (cross-tab beacon), so an idle tab never signs out a user
    // who is actively working in another tab of the same session.
    const effectiveActivity = Math.max(this.lastActivity, this.readBeacon());
    if (now - effectiveActivity >= minutes * 60_000) {
      this.openWarning();
    }
  }

  /** Best-effort cross-tab activity beacon (localStorage can be unavailable/full — never throw). */
  private writeBeacon(atMs: number): void {
    try {
      localStorage.setItem(ACTIVITY_STORAGE_KEY, String(atMs));
    } catch {
      // Non-fatal: cross-tab sync degrades to per-tab idle tracking.
    }
  }

  private readBeacon(): number {
    try {
      const raw = localStorage.getItem(ACTIVITY_STORAGE_KEY);
      const value = raw === null ? 0 : Number(raw);
      // Guard against clock skew / garbage: a future or non-finite beacon never counts as activity.
      return Number.isFinite(value) && value <= Date.now() ? value : 0;
    } catch {
      return 0;
    }
  }

  private openWarning(): void {
    this.warningStartedAt = Date.now();
    this.remainingSeconds.set(this.branding.idleWarningSeconds());

    const data: IdleWarningDialogData = { remainingSeconds: this.remainingSeconds.asReadonly() };
    this.warningRef = this.dialog.open(IdleWarningDialogComponent, {
      data,
      width: '420px',
      disableClose: true,
    });
    this.warningRef.afterClosed().subscribe((stay) => {
      this.warningRef = null;
      if (stay === true) {
        this.lastActivity = Date.now();
        this.writeBeacon(this.lastActivity); // reset the other tabs' watchdogs too
      } else if (stay === false) {
        this.signOut();
      }
      // undefined = closed by the countdown expiry path, which handles the sign-out itself.
    });
  }

  private closeWarning(): void {
    if (this.warningRef) {
      const ref = this.warningRef;
      this.warningRef = null;
      ref.close();
    }
  }

  private signOut(): void {
    this.lastActivity = Date.now();
    this.auth.logout().subscribe({
      next: () => {
        this.notify.info(this.i18n.translate('idle.signedOut'));
        void this.router.navigate(['/login']);
      },
      error: () => {
        // Even if the call fails, clear local state and return to login (mirrors the shell's logout).
        this.auth.clearSession();
        void this.router.navigate(['/login']);
      },
    });
  }
}
