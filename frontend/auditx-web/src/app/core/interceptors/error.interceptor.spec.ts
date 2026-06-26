import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Router } from '@angular/router';

import { errorInterceptor } from './error.interceptor';
import { AuthService } from '../services/auth.service';
import { NotificationService } from '../services/notification.service';
import { ProblemDetails } from '../models';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: jasmine.SpyObj<AuthService>;
  let notify: jasmine.SpyObj<NotificationService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['clearSession']);
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'error',
      'warning',
      'info',
      'success',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: NotificationService, useValue: notify },
        { provide: Router, useValue: router },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('on 401 (non-probe) clears session, warns and redirects to /login', () => {
    http.get('/api/v1/users').subscribe({ error: () => {} });
    httpMock
      .expectOne('/api/v1/users')
      .flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(auth.clearSession).toHaveBeenCalled();
    expect(notify.warning).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('on 401 from the session probe does not redirect', () => {
    http.get('/api/v1/auth/session').subscribe({ error: () => {} });
    httpMock
      .expectOne('/api/v1/auth/session')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.clearSession).toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(notify.warning).not.toHaveBeenCalled();
  });

  it('surfaces RFC 7807 title, detail and field errors as an error toast', () => {
    const problem: ProblemDetails = {
      title: 'Validation failed',
      detail: 'The request was invalid.',
      status: 422,
      field_errors: [{ field: 'name', code: 'required', message: 'Name is required' }],
    };

    http.post('/api/v1/roles', {}).subscribe({ error: () => {} });
    httpMock
      .expectOne('/api/v1/roles')
      .flush(problem, { status: 422, statusText: 'Unprocessable Entity' });

    expect(notify.error).toHaveBeenCalled();
    const msg = notify.error.calls.mostRecent().args[0];
    expect(msg).toContain('Validation failed');
    expect(msg).toContain('The request was invalid.');
    expect(msg).toContain('name: Name is required');
  });

  it('shows a connectivity message on status 0', () => {
    http.get('/api/v1/users').subscribe({ error: () => {} });
    httpMock
      .expectOne('/api/v1/users')
      .error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });

    expect(notify.error).toHaveBeenCalledWith(
      jasmine.stringMatching(/Unable to reach the server/),
    );
  });
});
