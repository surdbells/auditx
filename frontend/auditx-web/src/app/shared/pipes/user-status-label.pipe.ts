import { Pipe, PipeTransform } from '@angular/core';
import { UserStatus } from '../../core/models';

const LABELS: Record<UserStatus, string> = {
  active: 'Active',
  deactivated: 'Deactivated',
  awaiting_role_assignment: 'Awaiting role',
  locked: 'Locked',
};

@Pipe({ name: 'userStatusLabel' })
export class UserStatusLabelPipe implements PipeTransform {
  transform(status: UserStatus | string | null | undefined): string {
    if (!status) {
      return 'Unknown';
    }
    return LABELS[status as UserStatus] ?? status;
  }
}
