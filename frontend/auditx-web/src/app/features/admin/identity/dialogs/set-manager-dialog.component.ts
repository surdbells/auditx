import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UserLookupService } from '../../../../core/services/user-lookup.service';

export interface SetManagerDialogData {
  userDisplayName: string;
  /** The user whose manager is being set — excluded from the picker (can't manage themselves). */
  userId: string;
  currentManagerId: string | null;
}

/** Result: the chosen line-manager id, or null to clear it. */
export interface SetManagerResult {
  managerId: string | null;
}

/** Sets (or clears) a user's explicit line manager for reporting-line workflows. */
@Component({
  selector: 'app-set-manager-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'identity.manager.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">
        {{ 'identity.manager.hintBefore' | t
        }}<strong>{{ data.userDisplayName }}</strong
        >{{ 'identity.manager.hintAfter' | t }}
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.manager.label' | t }}</mat-label>
          <mat-select formControlName="managerId">
            <mat-option [value]="null">{{ 'identity.manager.none' | t }}</mat-option>
            @for (u of options(); track u.id) {
              <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
            }
          </mat-select>
          <mat-hint>{{ 'identity.manager.hint' | t }}</mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'identity.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()">
        {{ 'identity.manager.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 340px;
    }
    .full {
      width: 100%;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class SetManagerDialogComponent {
  readonly data = inject<SetManagerDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<SetManagerDialogComponent, SetManagerResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly userLookup = inject(UserLookupService);

  /** Directory options minus the user themselves (a user can't be their own manager). */
  readonly options = computed(() =>
    this.userLookup.options().filter((u) => u.id !== this.data.userId),
  );

  readonly form = this.fb.group({
    managerId: this.fb.control<string | null>(this.data.currentManagerId),
  });

  constructor() {
    this.userLookup.ensureLoaded();
  }

  submit(): void {
    this.dialogRef.close({ managerId: this.form.controls.managerId.value ?? null });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
