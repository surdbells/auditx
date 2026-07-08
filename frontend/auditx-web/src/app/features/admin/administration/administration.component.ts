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
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { BankSettingsComponent } from './bank-settings/bank-settings.component';
import { BulkUsersComponent } from './bulk-users/bulk-users.component';
import { SupportChannelComponent } from './support-channel/support-channel.component';
import { ReleasesComponent } from './releases/releases.component';
import { BackupRestoreComponent } from './backup-restore/backup-restore.component';
import { SystemHealthComponent } from './system-health/system-health.component';

@Component({
  selector: 'app-administration',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    PageHeaderComponent,
    TranslatePipe,
    BankSettingsComponent,
    BulkUsersComponent,
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

  readonly canViewSettings = computed(() =>
    this.auth.hasAnyPermission(
      Permissions.ViewBankSettings,
      Permissions.ManageBankSettings,
      Permissions.ConfigureLimits,
    ),
  );
  readonly canManageUsers = computed(() =>
    this.auth.hasPermission(Permissions.ManageUsers),
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
