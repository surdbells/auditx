import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatTableModule } from '@angular/material/table';

import { ExceptionRaisingRulesService } from '../../../core/services/exception-raising-rules.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { ExceptionRaisingRule, ResponseType } from '../../../core/models';
import {
  ExceptionRuleDialogComponent,
  ExceptionRuleDialogData,
  ExceptionRuleDialogResult,
} from './exception-rule-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const EXCEPTION_RULES_GUIDE: PageGuide = {
  id: 'exception-raising-rules',
  titleKey: 'exceptionRules.title',
  purposeKey: 'exceptionRules.guide.purpose',
  descriptionKey: 'exceptionRules.guide.description',
  actionKeys: ['exceptionRules.guide.action.create', 'exceptionRules.guide.action.edit'],
  sections: [
    {
      selector: '.exc-rules__table-card',
      titleKey: 'exceptionRules.guide.section.table.title',
      bodyKey: 'exceptionRules.guide.section.table.body',
    },
  ],
  usedByKeys: ['exceptionRules.guide.use.fieldwork'],
  businessRuleKeys: ['exceptionRules.guide.rule.failAlways', 'exceptionRules.guide.rule.onePerType'],
  tipKeys: ['exceptionRules.guide.tip.score'],
  permissionKeys: ['exceptionRules.guide.perm.manage'],
};

@Component({
  selector: 'app-exception-raising-rules',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './exception-raising-rules.component.html',
  styleUrl: './exception-raising-rules.component.scss',
})
export class ExceptionRaisingRulesComponent {
  private readonly service = inject(ExceptionRaisingRulesService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = ['responseType', 'allowOnNa', 'scoreThreshold', 'active', 'actions'];

  readonly state = signal<ViewState>('loading');
  readonly rules = signal<ExceptionRaisingRule[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageConfiguration),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.rules().length === 0,
  );

  readonly guide = EXCEPTION_RULES_GUIDE;

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.list().subscribe({
      next: (items) => {
        this.rules.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  responseTypeLabel(rt: ResponseType): string {
    const key = `audits.responseType.${rt}`;
    const label = this.i18n.translate(key);
    return label === key ? rt.replace(/_/g, ' ') : label;
  }

  create(): void {
    const data: ExceptionRuleDialogData = {
      existingResponseTypes: this.rules().map((r) => r.responseType),
    };
    this.dialog
      .open(ExceptionRuleDialogComponent, { data, width: '460px' })
      .afterClosed()
      .subscribe((result?: ExceptionRuleDialogResult) => {
        if (!result || result.mode !== 'create') {
          return;
        }
        this.service.create(result.body).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('exceptionRules.notify.created'));
            this.fetch();
          },
        });
      });
  }

  edit(rule: ExceptionRaisingRule): void {
    const data: ExceptionRuleDialogData = { rule, existingResponseTypes: [] };
    this.dialog
      .open(ExceptionRuleDialogComponent, { data, width: '460px' })
      .afterClosed()
      .subscribe((result?: ExceptionRuleDialogResult) => {
        if (!result || result.mode !== 'edit') {
          return;
        }
        this.service.update(result.id, result.body).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('exceptionRules.notify.updated'));
            this.fetch();
          },
        });
      });
  }
}
