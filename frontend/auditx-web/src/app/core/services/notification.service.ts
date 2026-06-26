import { Injectable, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';

/** Centralised Material snackbar/toast notifications. */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.show(message, 'auditx-snack-success');
  }

  info(message: string): void {
    this.show(message, 'auditx-snack-info');
  }

  warning(message: string): void {
    this.show(message, 'auditx-snack-warning', 7000);
  }

  error(message: string): void {
    this.show(message, 'auditx-snack-error', 8000);
  }

  private show(message: string, panelClass: string, duration = 4000): void {
    const config: MatSnackBarConfig = {
      duration,
      horizontalPosition: 'right',
      verticalPosition: 'top',
      panelClass: [panelClass],
    };
    this.snackBar.open(message, 'Dismiss', config);
  }
}
