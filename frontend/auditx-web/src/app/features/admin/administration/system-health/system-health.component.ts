import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

import { AdministrationService } from '../../../../core/services/administration.service';
import { SystemHealth } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-system-health',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
  ],
  templateUrl: './system-health.component.html',
  styleUrl: './system-health.component.scss',
})
export class SystemHealthComponent {
  private readonly admin = inject(AdministrationService);
  private readonly i18n = inject(TranslationService);

  readonly state = signal<ViewState>('loading');
  readonly health = signal<SystemHealth | null>(null);

  readonly metrics = computed(() => {
    // Track the active language so labels re-resolve on toggle.
    this.i18n.lang();
    const h = this.health();
    if (!h) {
      return [];
    }
    return [
      {
        icon: 'group',
        label: this.i18n.translate('administration.health.activeUsers'),
        value: h.activeUserCount,
      },
      {
        icon: 'groups',
        label: this.i18n.translate('administration.health.totalUsers'),
        value: h.totalUserCount,
      },
      {
        icon: 'description',
        label: this.i18n.translate('administration.health.templates'),
        value: h.templateCount,
      },
      {
        icon: 'hub',
        label: this.i18n.translate('administration.health.integrations'),
        value: h.integrationCount,
      },
    ];
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.admin.getSystemHealth().subscribe({
      next: (h) => {
        this.health.set(h);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
