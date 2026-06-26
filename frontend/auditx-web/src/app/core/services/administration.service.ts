import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  BankSettings,
  BulkOperationResult,
  CreateObjectRestoreRequest,
  CreateRestoreDrillRequest,
  DecideObjectRestoreRequest,
  EnableSupportChannelRequest,
  InstallReleaseRequest,
  ObjectRestoreRequest,
  ReleaseInstall,
  ResourceLimits,
  RestoreDrill,
  SupportChannelStatus,
  SystemHealth,
  UpdateBankSettingsRequest,
} from '../models';

/** Typed client for the M15 Administration console endpoints. */
@Injectable({ providedIn: 'root' })
export class AdministrationService {
  private readonly api = inject(ApiService);

  /* ---- Bank settings & resource limits ---- */

  getBankSettings(): Observable<BankSettings> {
    return this.api.get<BankSettings>('/admin/bank-settings');
  }

  updateBankSettings(
    body: UpdateBankSettingsRequest,
  ): Observable<BankSettings> {
    return this.api.patch<BankSettings>('/admin/bank-settings', body);
  }

  updateResourceLimits(body: ResourceLimits): Observable<ResourceLimits> {
    return this.api.patch<ResourceLimits>('/admin/resource-limits', body);
  }

  /* ---- Bulk users ---- */

  bulkDeactivateUsers(userIds: string[]): Observable<BulkOperationResult> {
    return this.api.post<BulkOperationResult>('/admin/users/bulk-deactivate', {
      userIds,
    });
  }

  bulkImportUsers(csvContent: string): Observable<BulkOperationResult> {
    return this.api.post<BulkOperationResult>('/admin/users/bulk-import', {
      csvContent,
    });
  }

  /* ---- Support channel ---- */

  getSupportChannel(): Observable<SupportChannelStatus> {
    return this.api.get<SupportChannelStatus>('/admin/support-channel');
  }

  enableSupportChannel(
    body: EnableSupportChannelRequest,
  ): Observable<SupportChannelStatus> {
    return this.api.post<SupportChannelStatus>(
      '/admin/support-channel/enable',
      body,
    );
  }

  revokeSupportChannel(): Observable<void> {
    return this.api.postVoid('/admin/support-channel/revoke');
  }

  /* ---- Releases ---- */

  listReleases(): Observable<ReleaseInstall[]> {
    return this.api.get<ReleaseInstall[]>('/admin/releases');
  }

  installRelease(body: InstallReleaseRequest): Observable<ReleaseInstall> {
    return this.api.post<ReleaseInstall>('/admin/releases/install', body);
  }

  /* ---- Backup / restore ---- */

  listRestoreDrills(): Observable<RestoreDrill[]> {
    return this.api.get<RestoreDrill[]>('/admin/restore-drills');
  }

  createRestoreDrill(
    body: CreateRestoreDrillRequest,
  ): Observable<RestoreDrill> {
    return this.api.post<RestoreDrill>('/admin/restore-drills', body);
  }

  requestObjectRestore(
    body: CreateObjectRestoreRequest,
  ): Observable<ObjectRestoreRequest> {
    return this.api.post<ObjectRestoreRequest>('/admin/restore/object', body);
  }

  decideObjectRestore(
    id: string,
    body: DecideObjectRestoreRequest,
  ): Observable<ObjectRestoreRequest> {
    return this.api.post<ObjectRestoreRequest>(
      `/admin/restore/object/${id}/decide`,
      body,
    );
  }

  /* ---- System health ---- */

  getSystemHealth(): Observable<SystemHealth> {
    return this.api.get<SystemHealth>('/admin/system-health');
  }
}
