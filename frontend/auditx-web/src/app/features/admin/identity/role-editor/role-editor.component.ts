import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';

import { RolesService } from '../../../../core/services/roles.service';
import { NotificationService } from '../../../../core/services/notification.service';
import {
  PermissionDto,
  PermissionScopeType,
  RoleDto,
  RolePermissionDto,
  SaveRoleRequest,
} from '../../../../core/models';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/** A selectable permission row with its current selection + scope. */
interface PermissionSelection {
  permission: PermissionDto;
  selected: boolean;
  scopeType: PermissionScopeType;
}

interface PermissionGroup {
  module: string;
  permissions: PermissionSelection[];
}

const SCOPE_ORDER: PermissionScopeType[] = [
  'global',
  'business_unit',
  'branch',
  'self',
];

const SCOPE_LABELS: Record<PermissionScopeType, string> = {
  global: 'Global',
  business_unit: 'Business unit',
  branch: 'Branch',
  self: 'Self',
};

@Component({
  selector: 'app-role-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './role-editor.component.html',
  styleUrl: './role-editor.component.scss',
})
export class RoleEditorComponent {
  /** Route param: 'new' for create, otherwise the role id. */
  readonly id = input<string>('new');

  private readonly rolesService = inject(RolesService);
  private readonly notify = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly state = signal<ViewState>('loading');
  readonly saving = signal(false);
  readonly catalogue = signal<PermissionDto[]>([]);
  readonly availableParents = signal<RoleDto[]>([]);
  readonly groups = signal<PermissionGroup[]>([]);
  readonly editingRole = signal<RoleDto | null>(null);

  // Missing/empty route param means create-mode too: withComponentInputBinding() does not
  // preserve the input default on the paramless `/admin/roles/new` route (it pushes undefined).
  readonly isNew = computed(() => {
    const id = this.id();
    return !id || id === 'new';
  });
  readonly isReadOnly = computed(() => this.editingRole()?.isBuiltIn ?? false);
  readonly scopeLabels = SCOPE_LABELS;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.maxLength(500)]],
    parentRoleIds: [[] as string[]],
  });

  readonly selectedCount = computed(() =>
    this.groups().reduce(
      (total, g) => total + g.permissions.filter((p) => p.selected).length,
      0,
    ),
  );

  constructor() {
    queueMicrotask(() => this.bootstrap());
  }

  private bootstrap(): void {
    this.state.set('loading');
    const id = this.id();
    forkJoin({
      catalogue: this.rolesService.permissionsCatalogue(),
      roles: this.rolesService.list(false),
      role: this.isNew()
        ? Promise.resolve(null)
        : this.rolesService.getById(id),
    }).subscribe({
      next: ({ catalogue, roles, role }) => {
        this.catalogue.set(catalogue);
        this.editingRole.set(role);
        // Parents cannot include the role itself (no self-inheritance).
        this.availableParents.set(roles.filter((r) => r.id !== id));
        this.buildGroups(catalogue, role);
        if (role) {
          this.form.patchValue({
            name: role.name,
            description: role.description,
            parentRoleIds: role.parentRoleIds,
          });
          if (role.isBuiltIn) {
            this.form.disable();
          }
        }
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  private buildGroups(
    catalogue: PermissionDto[],
    role: RoleDto | null,
  ): void {
    const existing = new Map<string, RolePermissionDto>(
      (role?.permissions ?? []).map((p) => [p.key, p]),
    );

    const byModule = new Map<string, PermissionSelection[]>();
    for (const permission of catalogue) {
      const current = existing.get(permission.key);
      const selection: PermissionSelection = {
        permission,
        selected: !!current,
        scopeType: current?.scopeType ?? permission.finestScope,
      };
      const list = byModule.get(permission.module) ?? [];
      list.push(selection);
      byModule.set(permission.module, list);
    }

    const groups: PermissionGroup[] = [...byModule.entries()]
      .map(([module, permissions]) => ({ module, permissions }))
      .sort((a, b) => a.module.localeCompare(b.module));

    this.groups.set(groups);
  }

  /** Scope options a permission may be constrained to (down to its finest scope). */
  scopeOptions(permission: PermissionDto): PermissionScopeType[] {
    const finestIndex = SCOPE_ORDER.indexOf(permission.finestScope);
    const upper = finestIndex === -1 ? SCOPE_ORDER.length - 1 : finestIndex;
    return SCOPE_ORDER.slice(0, upper + 1);
  }

  togglePermission(selection: PermissionSelection, checked: boolean): void {
    selection.selected = checked;
    // Trigger recompute of selectedCount.
    this.groups.update((g) => [...g]);
  }

  setScope(selection: PermissionSelection, scope: PermissionScopeType): void {
    selection.scopeType = scope;
  }

  groupSelectedCount(group: PermissionGroup): number {
    return group.permissions.filter((p) => p.selected).length;
  }

  cancel(): void {
    void this.router.navigate(['/admin/roles']);
  }

  save(): void {
    if (this.isReadOnly()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const permissions: RolePermissionDto[] = this.groups()
      .flatMap((g) => g.permissions)
      .filter((p) => p.selected)
      .map((p) => ({
        key: p.permission.key,
        scopeType: p.scopeType,
        scopePredicateJson: null,
      }));

    if (permissions.length === 0) {
      this.notify.warning('Select at least one permission for this role.');
      return;
    }

    const { name, description, parentRoleIds } = this.form.getRawValue();
    const payload: SaveRoleRequest = {
      name: name.trim(),
      description: description.trim(),
      permissions,
      parentRoleIds,
    };

    this.saving.set(true);
    const request$ = this.isNew()
      ? this.rolesService.create(payload)
      : this.rolesService.update(this.id(), payload);

    request$.subscribe({
      next: (result) => {
        this.saving.set(false);
        if (result.kind === 'pending') {
          this.notify.info(
            'Submitted for maker-checker approval. The change will apply once a second authoriser approves it.',
          );
        } else {
          this.notify.success(
            this.isNew() ? 'Role created.' : 'Role updated.',
          );
        }
        void this.router.navigate(['/admin/roles']);
      },
      error: () => this.saving.set(false),
    });
  }
}
