import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { InstitutionSettingsComponent } from './institution-settings/institution-settings.component';
import { BulkUsersComponent } from './bulk-users/bulk-users.component';
import { MakerCheckerGatesComponent } from './maker-checker-gates/maker-checker-gates.component';
import { SupportChannelComponent } from './support-channel/support-channel.component';
import { ReleasesComponent } from './releases/releases.component';
import { BackupRestoreComponent } from './backup-restore/backup-restore.component';
import { SystemHealthComponent } from './system-health/system-health.component';

/** Contextual page guide for the Administration hub (overview panel + walkthrough). */
const ADMINISTRATION_GUIDE: PageGuide = {
  id: 'administration-hub',
  titleKey: 'administration.page.title',
  purposeKey: 'administration.hub.guide.purpose',
  descriptionKey: 'administration.hub.guide.description',
  actionKeys: [
    'administration.hub.guide.action.settings',
    'administration.hub.guide.action.import',
    'administration.hub.guide.action.dualControl',
    'administration.hub.guide.action.ops',
  ],
  tipKeys: [
    'administration.hub.guide.tip.tabs',
    'administration.hub.guide.tip.permissions',
  ],
};

@Component({
  selector: 'app-administration',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
    InstitutionSettingsComponent,
    BulkUsersComponent,
    MakerCheckerGatesComponent,
    SupportChannelComponent,
    ReleasesComponent,
    BackupRestoreComponent,
    SystemHealthComponent,
  ],
  templateUrl: './administration.component.html',
  styles: `
    .tab-body {
      padding-top: 1.5rem;
    }
  `,
})
export class AdministrationComponent {
  private readonly auth = inject(AuthService);

  readonly guide = ADMINISTRATION_GUIDE;

  readonly canViewSettings = computed(() =>
    this.auth.hasAnyPermission(
      Permissions.ViewInstitutionSettings,
      Permissions.ManageInstitutionSettings,
      Permissions.ConfigureLimits,
    ),
  );
  readonly canManageUsers = computed(() =>
    this.auth.hasPermission(Permissions.ManageUsers),
  );
  readonly canManageDualControl = computed(() =>
    this.auth.hasPermission(Permissions.ManageInstitutionSettings),
  );
  readonly canManageSupport = computed(() =>
    this.auth.hasPermission(Permissions.ManageSupportChannel),
  );
  readonly canInstallReleases = computed(() =>
    this.auth.hasPermission(Permissions.InstallReleases),
  );
  readonly canBackupRestore = computed(() =>
    this.auth.hasAnyPermission(
      Permissions.ManageRetention,
      Permissions.ExecRestore,
    ),
  );
  readonly canViewHealth = computed(() =>
    this.auth.hasPermission(Permissions.ViewSystemHealth),
  );
}
