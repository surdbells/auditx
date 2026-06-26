import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-not-found',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <div class="nf">
      <div class="nf__code">404</div>
      <h1 class="nf__title">Page not found</h1>
      <p class="nf__message">
        The page you are looking for doesn't exist or you don't have access to it.
      </p>
      <a matButton="filled" routerLink="/dashboard">
        <mat-icon>home</mat-icon>
        Back to dashboard
      </a>
    </div>
  `,
  styles: `
    .nf {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 70vh;
      text-align: center;
      gap: 0.5rem;
      padding: 2rem;
    }
    .nf__code {
      font-size: 5rem;
      font-weight: 700;
      line-height: 1;
      color: var(--mat-sys-primary);
    }
    .nf__title {
      margin: 0.5rem 0 0;
      font: var(--mat-sys-headline-small);
    }
    .nf__message {
      margin: 0 0 1rem;
      max-width: 42ch;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class NotFoundComponent {}
