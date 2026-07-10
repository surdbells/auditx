import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MatDialog } from '@angular/material/dialog';

import { SavedViewsBarComponent } from './saved-views-bar.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { NotificationService } from '../../../core/services/notification.service';
import { SavedView } from '../../../core/models';

const BASE = '/api/v1';

function view(overrides: Partial<SavedView> = {}): SavedView {
  return {
    id: 'v-1',
    ownerUserId: 'u-1',
    viewKey: 'exceptions',
    name: 'My criticals',
    parametersJson: '{"severity":"critical","overdue":"yes"}',
    isShared: false,
    isOwner: true,
    version: 'AAAA',
    ...overrides,
  };
}

describe('SavedViewsBarComponent', () => {
  let fixture: ComponentFixture<SavedViewsBarComponent>;
  let component: SavedViewsBarComponent;
  let http: HttpTestingController;

  async function setup(rows: SavedView[]): Promise<void> {
    const dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    const notify = jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'error', 'info', 'warning']);

    TestBed.configureTestingModule({
      imports: [SavedViewsBarComponent],
      providers: [
        provideTestEnv(),
        { provide: MatDialog, useValue: dialog },
        { provide: NotificationService, useValue: notify },
      ],
    });
    fixture = TestBed.createComponent(SavedViewsBarComponent);
    fixture.componentRef.setInput('viewKey', 'exceptions');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();

    http.expectOne((r) => r.url === `${BASE}/saved-views`).flush({ data: rows });
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads the screen views on init', async () => {
    await setup([view(), view({ id: 'v-2', name: 'Team overdue', isShared: true })]);
    expect(component.views().length).toBe(2);
  });

  it('applies a view by emitting its parsed parameters', async () => {
    await setup([view()]);
    const emitted: Record<string, unknown>[] = [];
    component.applied.subscribe((p) => emitted.push(p));

    component.apply(component.views()[0]);
    expect(emitted.length).toBe(1);
    expect(emitted[0]['severity']).toBe('critical');
    expect(emitted[0]['overdue']).toBe('yes');
  });

  it('deletes an owned view then reloads', async () => {
    await setup([view()]);
    component.remove(component.views()[0], new Event('click'));

    http.expectOne((r) => r.url.startsWith(`${BASE}/saved-views/v-1`) && r.method === 'DELETE')
      .flush(null, { status: 204, statusText: 'No Content' });
    // The reload after delete.
    http.expectOne((r) => r.url === `${BASE}/saved-views`).flush({ data: [] });
    expect(component.views().length).toBe(0);
  });
});
