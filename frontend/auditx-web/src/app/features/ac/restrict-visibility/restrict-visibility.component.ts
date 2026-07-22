import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { AcService } from '../../../core/services/ac.service';
import { UsersService } from '../../../core/services/users.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { UserDto } from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for restricting a finding's visibility (drives the walkthrough + the About panel). */
const RESTRICT_VISIBILITY_GUIDE: PageGuide = {
  id: 'ac-restrict-visibility',
  titleKey: 'ac.restrictVisibility.title',
  purposeKey: 'ac.restrictVisibility.guide.purpose',
  descriptionKey: 'ac.restrictVisibility.guide.description',
  actionKeys: ['ac.restrictVisibility.guide.action.select', 'ac.restrictVisibility.guide.action.apply'],
  sections: [
    { selector: '.restrict__form', titleKey: 'ac.restrictVisibility.guide.section.form.title', bodyKey: 'ac.restrictVisibility.guide.section.form.body' },
  ],
  businessRuleKeys: ['ac.restrictVisibility.guide.rule.default', 'ac.restrictVisibility.guide.rule.filtered'],
  tipKeys: ['ac.restrictVisibility.guide.tip.reason'],
  permissionKeys: ['ac.restrictVisibility.guide.perm.cia'],
};

/**
 * CIA: set the per-finding visibility allow-list. Users on the list see the
 * finding's detail; everyone else sees the restricted placeholder. The backend
 * applies the filter per requester at read time.
 */
@Component({
  selector: 'app-ac-restrict-visibility',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './restrict-visibility.component.html',
  styleUrl: './restrict-visibility.component.scss',
})
export class AcRestrictVisibilityComponent {
  /** Route params bound via withComponentInputBinding. */
  readonly type = input.required<string>();
  readonly id = input.required<string>();

  private readonly service = inject(AcService);
  private readonly users = inject(UsersService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);

  readonly state = signal<ViewState>('loading');
  readonly userList = signal<UserDto[]>([]);
  readonly saving = signal(false);

  allowedUserIds: string[] = [];
  reason = '';

  readonly canRestrict = computed(() =>
    this.auth.hasPermission(Permissions.CIA),
  );

  readonly guide = RESTRICT_VISIBILITY_GUIDE;

  constructor() {
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set('loading');
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        this.userList.set(page.items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  save(): void {
    if (!this.canRestrict() || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.service
      .restrictFindingVisibility(this.type(), this.id(), {
        allowedUserIds: this.allowedUserIds,
        reason: this.reason.trim() || null,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.notify.success('Finding visibility updated.');
        },
        error: () => {
          this.saving.set(false);
          this.notify.error('We could not update the finding visibility.');
        },
      });
  }
}