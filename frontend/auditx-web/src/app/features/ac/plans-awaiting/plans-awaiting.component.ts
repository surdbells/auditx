import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { AnnualPlansService } from '../../../core/services/annual-plans.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { PlanListItem } from '../../../core/models';
import { humanise } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AcCommentsComponent } from '../components/ac-comments/ac-comments.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Plan statuses that are awaiting an AC-chair decision. */
const AWAITING: ('submitted' | 'revision_submitted')[] = [
  'submitted',
  'revision_submitted',
];

/**
 * Plans awaiting an AC-chair decision. Links to the EXISTING plan-detail
 * decision action (we do not rebuild it) and offers an AC-comments box per plan.
 */
@Component({
  selector: 'app-ac-plans-awaiting',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatExpansionModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    AcCommentsComponent,
  ],
  templateUrl: './plans-awaiting.component.html',
  styleUrl: './plans-awaiting.component.scss',
})
export class AcPlansAwaitingComponent {
  private readonly service = inject(AnnualPlansService);
  private readonly auth = inject(AuthService);

  readonly state = signal<ViewState>('loading');
  readonly plans = signal<PlanListItem[]>([]);

  readonly humanise = humanise;

  readonly canDecide = computed(() =>
    this.auth.hasPermission(Permissions.ACChair),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.plans().length === 0,
  );

  constructor() {
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set('loading');
    // The plans list filters by a single status, so fetch each awaiting bucket.
    forkJoin(
      AWAITING.map((status) =>
        this.service.list({ status, pageSize: 0 }),
      ),
    ).subscribe({
      next: (pages) => {
        this.plans.set(pages.flatMap((p) => p.items));
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}