import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MatDialog } from '@angular/material/dialog';

import { ItemReviewComponent } from './item-review.component';
import { WorkingPaperReviewStore } from './working-paper-review.store';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ChecklistProgressItem, ReviewItem, SessionDto, WorkingPaperReview } from '../../../core/models';

function session(userId: string): SessionDto {
  return {
    userId,
    email: 'a@b.c',
    firstName: 'A',
    lastName: 'B',
    displayName: 'A B',
    status: 'active',
    roles: [],
    permissions: ['ViewAudit'],
    expiresAt: '',
    absoluteExpiresAt: '',
  };
}

const item: ChecklistProgressItem = {
  itemId: 'i-1',
  sectionName: 'Controls',
  orderIndex: 0,
  prompt: 'Verify SoD',
  itemState: 'responded',
  verdict: 'pass',
  isRequired: true,
  assignedUserId: null,
  hasException: false,
  responseType: 'pass_fail_na',
};

function review(itemState: Partial<ReviewItem> = {}, overrides: Partial<WorkingPaperReview> = {}): WorkingPaperReview {
  return {
    isRequired: true,
    isOpen: true,
    canReview: true,
    canAnswer: true,
    totalItems: 1,
    signedOffItems: 0,
    readyForSignOff: 1,
    openNotes: 0,
    answeredNotes: 0,
    items: [
      { itemId: 'i-1', isAnswered: true, responderUserId: 'u-auditor', reviewedBy: null, reviewedAt: null, outstandingNotes: 0, ...itemState },
    ],
    notes: [],
    version: 'v1',
    ...overrides,
  };
}

describe('ItemReviewComponent — who may sign an item off', () => {
  let fixture: ComponentFixture<ItemReviewComponent>;
  let component: ItemReviewComponent;
  let store: WorkingPaperReviewStore;

  async function setup(me: string, r: WorkingPaperReview): Promise<void> {
    TestBed.configureTestingModule({
      imports: [ItemReviewComponent],
      providers: [
        provideTestEnv(),
        WorkingPaperReviewStore,
        { provide: MatDialog, useValue: jasmine.createSpyObj<MatDialog>('MatDialog', ['open']) },
        {
          provide: NotificationService,
          useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'info', 'warning', 'error']),
        },
      ],
    });
    TestBed.inject(AuthService).setSession(session(me));
    store = TestBed.inject(WorkingPaperReviewStore);
    store.review.set(r);
    fixture = TestBed.createComponent(ItemReviewComponent);
    fixture.componentRef.setInput('item', item);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  }

  afterEach(() => {
    // Rendering a name triggers the directory lookup; not what these specs are about.
    const http = TestBed.inject(HttpTestingController);
    http.match(() => true).forEach((req) => req.flush({ data: { items: [], total: 0 } }));
  });

  it('lets a reviewer sign off an answered item that someone else answered', async () => {
    await setup('u-lead', review());
    expect(component.canAct()).toBe(true);
    expect(component.canSignOff()).toBe(true);
    expect(component.answeredByMe()).toBe(false);
  });

  it('refuses the reviewer who answered the item, and says why', async () => {
    await setup('u-auditor', review());
    expect(component.answeredByMe()).toBe(true);
    expect(component.canSignOff()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('another reviewer must sign it off');
  });

  it('waits while a review note on the item is outstanding', async () => {
    await setup('u-lead', review({ outstandingNotes: 1 }));
    expect(component.canSignOff()).toBe(false);
  });

  it('cannot sign off an unanswered or already signed-off item', async () => {
    await setup('u-lead', review({ isAnswered: false, responderUserId: null }));
    expect(component.canSignOff()).toBe(false);

    store.review.set(review({ reviewedBy: 'u-lead', reviewedAt: '2026-02-01T10:00:00Z' }));
    expect(component.canSignOff()).toBe(false);
  });

  it('offers no review actions to a non-reviewer, or once the audit is closed', async () => {
    await setup('u-auditor', review({}, { canReview: false }));
    expect(component.canAct()).toBe(false);

    store.review.set(review({}, { isOpen: false }));
    expect(component.canAct()).toBe(false);
  });

  it('shows nothing for a self-assessment with no notes', async () => {
    await setup('u-lead', review({}, { isRequired: false }));
    expect(component.state()).toBeNull();
  });
});
