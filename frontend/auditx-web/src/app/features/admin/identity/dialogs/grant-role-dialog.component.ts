import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { GrantRoleRequest, RoleDto } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { OrgUnitLookupService } from '../../../../core/services/org-unit-lookup.service';
import { SearchableSelectComponent } from '../../../../shared/components/searchable-select/searchable-select.component';

export interface GrantRoleDialogData {
  userDisplayName: string;
  roles: RoleDto[];
}

@Component({
  selector: 'app-grant-role-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    SearchableSelectComponent,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'identity.grantRole.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">
        {{ 'identity.grantRole.hintBefore' | t
        }}<strong>{{ data.userDisplayName }}</strong
        >{{ 'identity.grantRole.hintAfter' | t }}
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.field.role' | t }}</mat-label>
          <mat-select formControlName="roleId">
            @for (role of data.roles; track role.id) {
              <mat-option [value]="role.id">{{ role.name }}</mat-option>
            }
          </mat-select>
          @if (form.controls.roleId.hasError('required') && form.controls.roleId.touched) {
            <mat-error>{{ 'identity.grantRole.roleRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <!-- Where the role applies: the whole organisation, or one org unit (and the units below it). Offered only for a
             role with permissions set to "Assigned org unit" — for any other role it would change nothing. -->
        @if (canLimit()) {
          <app-searchable-select
            class="full"
            formControlName="scopeValue"
            [label]="'identity.grantRole.scopeLabel' | t"
            [options]="orgUnitLookup.options()"
            [clearable]="true"
            [clearLabel]="'identity.grantRole.wholeOrganisation' | t"
          />
          <p class="scope-hint">{{ 'identity.grantRole.scopeHint' | t }}</p>
        } @else if (form.controls.roleId.value) {
          <p class="scope-hint">{{ 'identity.grantRole.organisationWide' | t }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'identity.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'identity.grantRole.title' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 360px;
    }
    .full {
      width: 100%;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    .scope-hint {
      margin: 0.25rem 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class GrantRoleDialogComponent {
  readonly data = inject<GrantRoleDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<GrantRoleDialogComponent, GrantRoleRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);
  readonly orgUnitLookup = inject(OrgUnitLookupService);

  readonly submitted = signal(false);

  readonly form = this.fb.nonNullable.group({
    roleId: ['', [Validators.required]],
    scopeValue: [''],
  });

  private readonly roleId = toSignal(this.form.controls.roleId.valueChanges, { initialValue: '' });

  /** Whether the chosen role (or a role it inherits from) has permissions limited to the assigned org unit. */
  readonly canLimit = computed(() => {
    const byId = new Map(this.data.roles.map((r) => [r.id, r]));
    const seen = new Set<string>();
    const stack = this.roleId() ? [this.roleId()] : [];
    while (stack.length) {
      const id = stack.pop()!;
      if (seen.has(id)) {
        continue;
      }
      seen.add(id);
      const role = byId.get(id);
      if (role?.permissions.some((p) => p.scopeType === 'org_unit')) {
        return true;
      }
      stack.push(...(role?.parentRoleIds ?? []));
    }
    return false;
  });

  constructor() {
    this.orgUnitLookup.ensureLoaded();
    // A unit chosen for a role that cannot be limited would be refused; clear it when the role changes.
    this.form.controls.roleId.valueChanges.subscribe(() => {
      if (!this.canLimit()) {
        this.form.controls.scopeValue.setValue('');
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { roleId, scopeValue } = this.form.getRawValue();
    const result: GrantRoleRequest = {
      roleId,
      scopeValue: this.canLimit() && scopeValue ? scopeValue : null,
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
