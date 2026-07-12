import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';

import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { MakerCheckerService } from '../../../../core/services/maker-checker.service';
import { RolesService } from '../../../../core/services/roles.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { MakerCheckerGateDto, RoleDto } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** i18n label/description keys per enforced action type (kept in sync with the backend catalogue). */
const ACTION_KEYS: Record<string, { label: string; desc: string }> = {
  role_permission_change: {
    label: 'administration.dualControl.action.rolePermissionChange.label',
    desc: 'administration.dualControl.action.rolePermissionChange.desc',
  },
  template_publish: {
    label: 'administration.dualControl.action.templatePublish.label',
    desc: 'administration.dualControl.action.templatePublish.desc',
  },
  map_approval: {
    label: 'administration.dualControl.action.mapApproval.label',
    desc: 'administration.dualControl.action.mapApproval.desc',
  },
  sanctions_grid_edit: {
    label: 'administration.dualControl.action.sanctionsGridEdit.label',
    desc: 'administration.dualControl.action.sanctionsGridEdit.desc',
  },
  config_activation: {
    label: 'administration.dualControl.action.configActivation.label',
    desc: 'administration.dualControl.action.configActivation.desc',
  },
};

/** Contextual page guide for the dual-control (maker-checker) gate configuration. */
const DUAL_CONTROL_GUIDE: PageGuide = {
  id: 'admin-dual-control',
  titleKey: 'administration.tabs.dualControl',
  purposeKey: 'administration.dualControl.guide.purpose',
  descriptionKey: 'administration.dualControl.guide.description',
  actionKeys: [
    'administration.dualControl.guide.action.toggle',
    'administration.dualControl.guide.action.role',
    'administration.dualControl.guide.action.self',
  ],
  sections: [
    { selector: '.gates__grid', titleKey: 'administration.dualControl.guide.section.gates.title', bodyKey: 'administration.dualControl.guide.section.gates.body' },
  ],
  workflowKeys: [
    'administration.dualControl.guide.flow.choose',
    'administration.dualControl.guide.flow.enable',
    'administration.dualControl.guide.flow.policy',
    'administration.dualControl.guide.flow.effect',
  ],
  dependsOnKeys: [
    'administration.dualControl.guide.dep.roles',
    'administration.dualControl.guide.dep.queue',
  ],
  usedByKeys: [
    'administration.dualControl.guide.use.roles',
    'administration.dualControl.guide.use.templates',
    'administration.dualControl.guide.use.config',
  ],
  businessRuleKeys: [
    'administration.dualControl.guide.rule.enforced',
    'administration.dualControl.guide.rule.self',
    'administration.dualControl.guide.rule.instance',
  ],
  tipKeys: [
    'administration.dualControl.guide.tip.role',
    'administration.dualControl.guide.tip.queue',
  ],
  permissionKeys: ['administration.dualControl.guide.perm.manage'],
  faq: [
    { questionKey: 'administration.dualControl.guide.faq.disable.q', answerKey: 'administration.dualControl.guide.faq.disable.a' },
    { questionKey: 'administration.dualControl.guide.faq.missing.q', answerKey: 'administration.dualControl.guide.faq.missing.a' },
  ],
};

/**
 * Admin screen to configure which actions are gated by maker-checker (dual control). Each enforced
 * action type has a toggle; when gated, an optional checker role restricts who may approve and a
 * second toggle governs whether the maker may also act as checker. Changes save immediately.
 */
@Component({
  selector: 'app-maker-checker-gates',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatSlideToggleModule,
    MatFormFieldModule,
    MatSelectModule,
    IconComponent,
    MatProgressBarModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    PageGuideComponent,
  ],
  templateUrl: './maker-checker-gates.component.html',
  styleUrl: './maker-checker-gates.component.scss',
})
export class MakerCheckerGatesComponent {
  private readonly service = inject(MakerCheckerService);
  private readonly rolesService = inject(RolesService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);

  readonly guide = DUAL_CONTROL_GUIDE;

  readonly state = signal<ViewState>('loading');
  readonly gates = signal<MakerCheckerGateDto[]>([]);
  readonly roles = signal<RoleDto[]>([]);
  /** Action types with an in-flight save — disables their controls. */
  readonly savingTypes = signal<ReadonlySet<string>>(new Set());

  readonly roleNames = computed(() => this.roles().map((r) => r.name));

  constructor() {
    this.rolesService.list(false).subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => this.roles.set([]),
    });
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.gates().subscribe({
      next: (gates) => {
        this.gates.set(gates);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  labelKey(actionType: string): string {
    return ACTION_KEYS[actionType]?.label ?? actionType;
  }
  descKey(actionType: string): string {
    return ACTION_KEYS[actionType]?.desc ?? '';
  }
  isSaving(actionType: string): boolean {
    return this.savingTypes().has(actionType);
  }

  toggleEnabled(gate: MakerCheckerGateDto, isEnabled: boolean): void {
    this.save(gate, { isEnabled });
  }
  toggleAllowMaker(gate: MakerCheckerGateDto, allowMakerAsChecker: boolean): void {
    this.save(gate, { allowMakerAsChecker });
  }
  setCheckerRole(gate: MakerCheckerGateDto, roleName: string): void {
    this.save(gate, { checkerRoleName: roleName === '' ? null : roleName });
  }

  /** Merge the patch onto a gate, persist it, and reconcile with the server response. */
  private save(gate: MakerCheckerGateDto, patch: Partial<MakerCheckerGateDto>): void {
    if (this.isSaving(gate.actionType)) {
      return;
    }
    const next: MakerCheckerGateDto = { ...gate, ...patch };
    this.setSaving(gate.actionType, true);
    // Optimistically reflect the change so the UI feels immediate.
    this.replaceGate(next);
    this.service
      .configureGate({
        actionType: next.actionType,
        isEnabled: next.isEnabled,
        checkerRoleName: next.checkerRoleName,
        allowMakerAsChecker: next.allowMakerAsChecker,
      })
      .subscribe({
        next: (saved) => {
          this.replaceGate(saved);
          this.setSaving(gate.actionType, false);
          this.notify.success(
            this.i18n.translate('administration.dualControl.saved', {
              action: this.i18n.translate(this.labelKey(gate.actionType)),
            }),
          );
        },
        error: () => {
          // Revert to the server's truth; the interceptor already surfaced the error.
          this.replaceGate(gate);
          this.setSaving(gate.actionType, false);
        },
      });
  }

  private replaceGate(gate: MakerCheckerGateDto): void {
    this.gates.update((list) =>
      list.map((g) => (g.actionType === gate.actionType ? gate : g)),
    );
  }

  private setSaving(actionType: string, on: boolean): void {
    this.savingTypes.update((s) => {
      const next = new Set(s);
      if (on) {
        next.add(actionType);
      } else {
        next.delete(actionType);
      }
      return next;
    });
  }
}
