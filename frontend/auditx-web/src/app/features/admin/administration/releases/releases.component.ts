import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';

import { AdministrationService } from '../../../../core/services/administration.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { ReleaseInstall } from '../../../../core/models';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-releases',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    ErrorStateComponent,
  ],
  templateUrl: './releases.component.html',
  styleUrl: './releases.component.scss',
})
export class ReleasesComponent {
  private readonly admin = inject(AdministrationService);
  private readonly notify = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  readonly displayedColumns = ['version', 'status', 'changeRecord', 'detail', 'createdAt'];

  readonly state = signal<ViewState>('loading');
  readonly releases = signal<ReleaseInstall[]>([]);
  readonly installing = signal(false);

  readonly form = this.fb.nonNullable.group({
    version: ['', Validators.required],
    manifestSha256: ['', Validators.required],
    changeRecordReference: ['', Validators.required],
    signatureBase64: ['', Validators.required],
    manifestContentBase64: ['', Validators.required],
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.admin.listReleases().subscribe({
      next: (items) => {
        this.releases.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  install(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.installing.set(true);
    this.admin
      .installRelease({
        version: v.version.trim(),
        manifestSha256: v.manifestSha256.trim(),
        changeRecordReference: v.changeRecordReference.trim(),
        signatureBase64: v.signatureBase64.trim(),
        manifestContentBase64: v.manifestContentBase64.trim(),
      })
      .subscribe({
        next: (result) => {
          if (result.status === 'Rejected') {
            this.notify.warning(
              `Release ${result.version} rejected: ${result.detail ?? 'verification failed'}`,
            );
          } else {
            this.notify.success(`Release ${result.version} ${result.status.toLowerCase()}.`);
            this.form.reset();
          }
          this.installing.set(false);
          this.fetch();
        },
        // 409 (signature/hash invalid) is surfaced by the error interceptor.
        error: () => this.installing.set(false),
      });
  }
}
