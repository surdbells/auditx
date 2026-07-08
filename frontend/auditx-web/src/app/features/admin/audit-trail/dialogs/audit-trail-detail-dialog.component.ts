import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
} from '@angular/material/dialog';

import { AuditTrailEntry } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  humaniseActorType,
  humaniseEventType,
  prettyJson,
} from '../humanise';

export interface AuditTrailDetailDialogData {
  entry: AuditTrailEntry;
}

@Component({
  selector: 'app-audit-trail-detail-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatDialogModule, MatButtonModule, TranslatePipe],
  templateUrl: './audit-trail-detail-dialog.component.html',
  styleUrl: './audit-trail-detail-dialog.component.scss',
})
export class AuditTrailDetailDialogComponent {
  readonly data = inject<AuditTrailDetailDialogData>(MAT_DIALOG_DATA);

  readonly entry = this.data.entry;

  readonly humaniseActorType = humaniseActorType;
  readonly humaniseEventType = humaniseEventType;
  readonly prettyJson = prettyJson;
}
