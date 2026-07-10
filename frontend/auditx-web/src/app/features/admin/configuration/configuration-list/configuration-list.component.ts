import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

import { CONFIG_DOMAIN_EXCEPTION_DEFAULTS } from '../../../../core/models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

/** A configurable domain surfaced as a navigable card. */
interface ConfigDomainCard {
  domain: string;
  title: string;
  description: string;
  icon: string;
}

/** Contextual page guide for the configuration domain list (drives the walkthrough + the About panel). */
const CONFIGURATION_GUIDE: PageGuide = {
  id: 'configuration-list',
  titleKey: 'config.list.title',
  purposeKey: 'config.list.guide.purpose',
  descriptionKey: 'config.list.guide.description',
  actionKeys: [
    'config.list.guide.action.browse',
    'config.list.guide.action.open',
    'config.list.guide.action.version',
  ],
  sections: [
    { selector: '.config-list__grid', titleKey: 'config.list.guide.section.grid.title', bodyKey: 'config.list.guide.section.grid.body' },
    { selector: '.config-list__card', titleKey: 'config.list.guide.section.card.title', bodyKey: 'config.list.guide.section.card.body' },
    { selector: '[data-guide="domain-code"]', titleKey: 'config.list.guide.section.code.title', bodyKey: 'config.list.guide.section.code.body' },
  ],
  workflowKeys: [
    'config.list.guide.flow.browse',
    'config.list.guide.flow.draft',
    'config.list.guide.flow.review',
    'config.list.guide.flow.activate',
    'config.list.guide.flow.apply',
  ],
  dependsOnKeys: [
    'config.list.guide.dep.identity',
    'config.list.guide.dep.makerChecker',
    'config.list.guide.dep.reference',
  ],
  usedByKeys: [
    'config.list.guide.use.findings',
    'config.list.guide.use.analytics',
    'config.list.guide.use.reports',
  ],
  businessRuleKeys: [
    'config.list.guide.rule.versioned',
    'config.list.guide.rule.reason',
    'config.list.guide.rule.gated',
    'config.list.guide.rule.rollback',
  ],
  tipKeys: [
    'config.list.guide.tip.reason',
    'config.list.guide.tip.rollback',
  ],
  permissionKeys: [
    'config.list.guide.perm.view',
    'config.list.guide.perm.manage',
  ],
  faq: [
    { questionKey: 'config.list.guide.faq.domains.q', answerKey: 'config.list.guide.faq.domains.a' },
    { questionKey: 'config.list.guide.faq.activate.q', answerKey: 'config.list.guide.faq.activate.a' },
  ],
};

/** Landing page listing the configuration domains that can be edited. */
@Component({
  selector: 'app-configuration-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './configuration-list.component.html',
  styleUrl: './configuration-list.component.scss',
})
export class ConfigurationListComponent {
  private readonly i18n = inject(TranslationService);

  readonly guide = CONFIGURATION_GUIDE;

  readonly domains: ConfigDomainCard[] = [
    {
      domain: CONFIG_DOMAIN_EXCEPTION_DEFAULTS,
      title: this.i18n.translate('config.list.exceptionDefaults.title'),
      description: this.i18n.translate(
        'config.list.exceptionDefaults.description',
      ),
      icon: 'tune',
    },
  ];
}
