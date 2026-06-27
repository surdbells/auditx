import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

import { CONFIG_DOMAIN_EXCEPTION_DEFAULTS } from '../../../../core/models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';

/** A configurable domain surfaced as a navigable card. */
interface ConfigDomainCard {
  domain: string;
  title: string;
  description: string;
  icon: string;
}

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
  ],
  templateUrl: './configuration-list.component.html',
  styleUrl: './configuration-list.component.scss',
})
export class ConfigurationListComponent {
  readonly domains: ConfigDomainCard[] = [
    {
      domain: CONFIG_DOMAIN_EXCEPTION_DEFAULTS,
      title: 'Exception defaults & SLAs',
      description:
        'Target remediation days per severity and the recurrence detection window and threshold.',
      icon: 'tune',
    },
  ];
}
