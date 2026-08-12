import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { IconComponent } from '../../../core/icons/icon.component';
import { ControlsService } from '../../../core/services/controls.service';
import { RisksService } from '../../../core/services/risks.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { ControlRiskLink, RiskListItem } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

export interface ControlRisksDialogData {
  controlId: string;
  controlCode: string;
  controlTitle: string;
}

/** Manage the many-to-many links between one control and the risks it mitigates. */
@Component({
  selector: 'app-control-risks-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'control.risks.title' | t }}</h2>
    <mat-dialog-content>
      <p class="subject">{{ data.controlCode }} — {{ data.controlTitle }}</p>

      @if (canManage()) {
        <form [formGroup]="form" class="add-row">
          <mat-form-field appearance="outline" class="grow">
            <mat-label>{{ 'control.risks.addLabel' | t }}</mat-label>
            <mat-select formControlName="riskId">
              @for (r of availableRisks(); track r.id) {
                <mat-option [value]="r.id">{{ r.title }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <button matButton="filled" type="button" [disabled]="!form.controls.riskId.value" (click)="link()">
            <app-icon name="add" />
            {{ 'control.risks.add' | t }}
          </button>
        </form>
      }

      @if (!links().length) {
        <p class="muted">{{ 'control.risks.empty' | t }}</p>
      } @else {
        <ul class="links">
          @for (l of links(); track l.linkId) {
            <li class="links__item">
              <div class="links__body">
                <span class="links__title">{{ l.title }}</span>
                <span class="muted links__meta">{{ l.category }} · {{ 'risk.status.' + l.status | t }}</span>
              </div>
              @if (canManage()) {
                <button matIconButton type="button" (click)="unlink(l)" [attr.aria-label]="'control.risks.remove' | t">
                  <app-icon name="link_off" />
                </button>
              }
            </li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="close()">{{ 'control.risks.close' | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .subject {
      margin: 0 0 1rem;
      font-weight: 500;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .add-row {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
      min-width: 440px;
    }
    .grow {
      flex: 1;
    }
    .links {
      list-style: none;
      margin: 0.5rem 0 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
    }
    .links__item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      padding: 0.4rem 0.6rem;
      border-radius: 8px;
      background: var(--mat-sys-surface-container-low);
    }
    .links__body {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .links__title {
      font-weight: 500;
    }
    .links__meta {
      font: var(--mat-sys-label-small);
    }
    @media (max-width: 520px) {
      .add-row {
        min-width: auto;
      }
    }
  `,
})
export class ControlRisksDialogComponent {
  readonly data = inject<ControlRisksDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ControlRisksDialogComponent>>(MatDialogRef);
  private readonly controls = inject(ControlsService);
  private readonly risks = inject(RisksService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly links = signal<ControlRiskLink[]>([]);
  readonly allRisks = signal<RiskListItem[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageControls),
  );

  /** Risks not already linked, for the add dropdown. */
  readonly availableRisks = computed(() => {
    const linked = new Set(this.links().map((l) => l.riskId));
    return this.allRisks().filter((r) => !linked.has(r.id));
  });

  readonly form = this.fb.nonNullable.group({
    riskId: [''],
  });

  constructor() {
    this.refresh();
    // Load-all cap; the risk register is small enough to list in a picker.
    this.risks.list({ page: 1, pageSize: 0 }).subscribe((page) => this.allRisks.set(page.items));
  }

  private refresh(): void {
    this.controls.listRisks(this.data.controlId).subscribe((rows) => this.links.set(rows));
  }

  link(): void {
    const riskId = this.form.controls.riskId.value;
    if (!riskId) {
      return;
    }
    this.controls.linkRisk(this.data.controlId, { riskId }).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('control.risks.linked'));
        this.form.reset();
        this.refresh();
      },
    });
  }

  unlink(link: ControlRiskLink): void {
    this.controls.unlinkRisk(this.data.controlId, link.riskId).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('control.risks.unlinked'));
        this.refresh();
      },
    });
  }

  close(): void {
    this.dialogRef.close();
  }
}
