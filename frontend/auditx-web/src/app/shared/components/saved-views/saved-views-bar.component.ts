import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';

import { SavedViewsService } from '../../../core/services/saved-views.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SavedView } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import {
  SaveViewDialogComponent,
  SaveViewResult,
} from './save-view-dialog.component';

/**
 * Reusable saved-views bar (D3-A). Drop onto any list/analytics screen: bind [viewKey] and the current filter
 * object as [params], and handle (applied) to re-apply a chosen view's parameters. Renders a menu of the caller's
 * own + shared views and a "Save current view" action. Applying a view only emits its stored parameters — the host
 * screen decides how to load them into its filter form.
 */
@Component({
  selector: 'app-saved-views-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatMenuModule, IconComponent, MatTooltipModule, TranslatePipe],
  templateUrl: './saved-views-bar.component.html',
  styleUrl: './saved-views-bar.component.scss',
})
export class SavedViewsBarComponent {
  /** Screen key that scopes which views apply here (e.g. 'exceptions'). */
  readonly viewKey = input.required<string>();
  /** The current filter selection to persist when saving a view. */
  readonly params = input<Record<string, unknown>>({});
  /** Emits a chosen view's parsed parameters for the host to apply to its filters. */
  readonly applied = output<Record<string, unknown>>();

  private readonly service = inject(SavedViewsService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly views = signal<SavedView[]>([]);
  readonly loading = signal(false);
  private lastKey = '';

  constructor() {
    effect(() => {
      const key = this.viewKey();
      if (key && key !== this.lastKey) {
        this.lastKey = key;
        this.refresh();
      }
    });
  }

  refresh(): void {
    this.loading.set(true);
    this.service.list(this.viewKey()).subscribe({
      next: (rows) => {
        this.views.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  apply(view: SavedView): void {
    try {
      const parsed = JSON.parse(view.parametersJson) as Record<string, unknown>;
      this.applied.emit(parsed ?? {});
    } catch {
      this.notify.error(this.i18n.translate('savedViews.notify.applyFailed'));
    }
  }

  saveCurrent(): void {
    this.dialog
      .open(SaveViewDialogComponent, {
        data: { title: this.i18n.translate('savedViews.saveTitle') },
        width: '440px',
      })
      .afterClosed()
      .subscribe((result?: SaveViewResult) => {
        if (!result) {
          return;
        }
        this.service
          .create({
            viewKey: this.viewKey(),
            name: result.name,
            parametersJson: JSON.stringify(this.params() ?? {}),
            isShared: result.isShared,
          })
          .subscribe({
            next: () => {
              this.notify.success(this.i18n.translate('savedViews.notify.saved'));
              this.refresh();
            },
          });
      });
  }

  remove(view: SavedView, event: Event): void {
    event.stopPropagation();
    this.service.delete(view.id, view.version).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('savedViews.notify.deleted'));
        this.refresh();
      },
    });
  }
}
