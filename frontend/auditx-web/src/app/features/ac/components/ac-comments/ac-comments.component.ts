import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';

import { AcService } from '../../../../core/services/ac.service';
import { UserLookupService } from '../../../../core/services/user-lookup.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { AcComment, AcCommentTargetType } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

/**
 * AC commentary box (BR-M13-010): a polymorphic comment thread attached to a
 * plan / pack / finding target. Posting requires the ACMember permission.
 * Re-loads whenever the bound target changes.
 */
@Component({
  selector: 'app-ac-comments',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    MatFormFieldModule,
    MatInputModule,
    TranslatePipe,
  ],
  templateUrl: './ac-comments.component.html',
  styleUrl: './ac-comments.component.scss',
})
export class AcCommentsComponent {
  readonly targetType = input.required<AcCommentTargetType>();
  readonly targetId = input.required<string>();

  private readonly service = inject(AcService);
  /** Resolves comment-author user ids to display names. */
  readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslationService);

  readonly comments = signal<AcComment[]>([]);
  readonly loading = signal(false);
  readonly posting = signal(false);
  draft = '';

  readonly canComment = computed(() =>
    this.auth.hasPermission(Permissions.ACMember),
  );

  constructor() {
    // Reload whenever the target identity changes.
    effect(() => {
      const type = this.targetType();
      const id = this.targetId();
      if (type && id) {
        this.load(type, id);
      }
    });
  }

  private load(type: AcCommentTargetType, id: string): void {
    this.loading.set(true);
    this.service.listComments(type, id).subscribe({
      next: (items) => {
        this.comments.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  post(): void {
    const text = this.draft.trim();
    if (!text || this.posting()) {
      return;
    }
    this.posting.set(true);
    this.service
      .addComment({
        targetType: this.targetType(),
        targetId: this.targetId(),
        comment: text,
      })
      .subscribe({
        next: (comment) => {
          this.comments.update((list) => [...list, comment]);
          this.draft = '';
          this.posting.set(false);
        },
        error: () => {
          this.posting.set(false);
          this.notify.error(this.i18n.translate('ac.comments.postError'));
        },
      });
  }
}