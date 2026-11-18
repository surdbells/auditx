import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';

import { IconComponent } from '../../../core/icons/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { ReviewNote } from '../../../core/models';
import { WorkingPaperReviewStore } from './working-paper-review.store';

/**
 * A list of review notes with what the signed-in user may do on each: the team answers an open note; a reviewer clears
 * an answered note or reopens it, and may withdraw an open one. With {@link itemPrompts}, each note says which item it
 * is about and links to it.
 */
@Component({
  selector: 'app-review-note-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, IconComponent, TranslatePipe],
  template: `
    <ul class="notes">
      @for (note of notes(); track note.id) {
        <li class="note" [attr.data-status]="note.status">
          <div class="note__head">
            <span class="note__status" [attr.data-status]="note.status">{{ 'audits.review.status.' + note.status | t }}</span>
            @if (itemPrompts(); as prompts) {
              @if (note.checklistItemId) {
                <button type="button" class="note__item" (click)="openItem.emit(note.checklistItemId)">
                  {{ prompts[note.checklistItemId] ?? ('audits.review.unknownItem' | t) }}
                </button>
              } @else {
                <span class="note__item note__item--audit">{{ 'audits.review.wholeAudit' | t }}</span>
              }
            }
          </div>

          <p class="note__body">{{ note.body }}</p>
          <p class="note__meta">
            {{ 'audits.review.raisedBy' | t: { name: nameOf(note.raisedBy), date: (note.raisedAt | date: 'medium') } }}
          </p>

          @if (note.answer) {
            <div class="note__answer">
              <p class="note__meta">
                {{ 'audits.review.answeredBy' | t: { name: nameOf(note.answeredBy), date: (note.answeredAt | date: 'medium') } }}
              </p>
              <p class="note__body">{{ note.answer }}</p>
            </div>
          }

          @if (note.status === 'cleared') {
            <p class="note__meta note__meta--cleared">
              <app-icon name="check_circle" aria-hidden="true" />
              {{ 'audits.review.clearedBy' | t: { name: nameOf(note.clearedBy), date: (note.clearedAt | date: 'medium') } }}
            </p>
          }

          @if (isOpen()) {
            <div class="note__actions">
              @if (note.status === 'open' && canAnswer()) {
                <button matButton="tonal" type="button" [disabled]="store.busy()" (click)="answer(note)">
                  <app-icon name="reply" />
                  {{ 'audits.review.action.answer' | t }}
                </button>
              }
              @if (note.status === 'answered' && canReview()) {
                <button matButton="tonal" type="button" [disabled]="store.busy()" (click)="store.clearNote(note)">
                  <app-icon name="done" />
                  {{ 'audits.review.action.clear' | t }}
                </button>
                <button matButton type="button" [disabled]="store.busy()" (click)="store.reopen(note)">
                  <app-icon name="undo" />
                  {{ 'audits.review.action.reopen' | t }}
                </button>
              }
              @if (note.status === 'open' && canReview()) {
                <button matButton type="button" [disabled]="store.busy()" (click)="store.clearNote(note)">
                  <app-icon name="done" />
                  {{ 'audits.review.action.withdraw' | t }}
                </button>
              }
            </div>
          }
        </li>
      }
    </ul>
  `,
  styles: `
    :host {
      display: block;
    }
    .notes {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .note {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding: 10px 12px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 10px;
      background: var(--mat-sys-surface);
    }
    .note[data-status='cleared'] {
      background: var(--mat-sys-surface-container-low);
    }
    .note__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      min-width: 0;
    }
    .note__status {
      display: inline-flex;
      align-items: center;
      padding: 1px 8px;
      border-radius: 999px;
      font-size: 0.72rem;
      font-weight: 600;
      letter-spacing: 0.02em;
      text-transform: uppercase;
    }
    .note__status[data-status='open'] {
      background: color-mix(in srgb, #ef6c00 16%, transparent);
      color: #b45309;
    }
    .note__status[data-status='answered'] {
      background: color-mix(in srgb, #1565c0 14%, transparent);
      color: #1d4ed8;
    }
    .note__status[data-status='cleared'] {
      background: color-mix(in srgb, #2e7d32 14%, transparent);
      color: #2e7d32;
    }
    :host-context([data-theme='dark']) .note__status[data-status='open'] {
      color: #fbbf24;
    }
    :host-context([data-theme='dark']) .note__status[data-status='answered'] {
      color: #93c5fd;
    }
    :host-context([data-theme='dark']) .note__status[data-status='cleared'] {
      color: #86efac;
    }
    .note__item {
      min-width: 0;
      max-width: 100%;
      padding: 0;
      border: 0;
      background: none;
      color: var(--mat-sys-primary);
      font: inherit;
      font-size: 0.85rem;
      text-align: left;
      cursor: pointer;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .note__item:hover,
    .note__item:focus-visible {
      text-decoration: underline;
    }
    .note__item--audit {
      color: var(--mat-sys-on-surface-variant);
      cursor: default;
    }
    .note__item--audit:hover {
      text-decoration: none;
    }
    .note__body {
      margin: 0;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      line-height: 1.45;
    }
    .note__meta {
      margin: 0;
      display: flex;
      align-items: center;
      gap: 4px;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.78rem;
    }
    .note__meta--cleared {
      color: #2e7d32;
    }
    :host-context([data-theme='dark']) .note__meta--cleared {
      color: #86efac;
    }
    .note__answer {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 8px 10px;
      border-left: 3px solid var(--mat-sys-outline-variant);
      border-radius: 0 6px 6px 0;
      background: var(--mat-sys-surface-container-low);
    }
    .note__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-top: 2px;
    }
  `,
})
export class ReviewNoteListComponent {
  readonly store = inject(WorkingPaperReviewStore);
  private readonly users = inject(UserLookupService);

  readonly notes = input.required<ReviewNote[]>();
  /** itemId → prompt. When given, each note names (and links to) its item. */
  readonly itemPrompts = input<Record<string, string | undefined> | null>(null);
  /** The prompt of the item these notes are on (for the answer dialog), when they are all on one item. */
  readonly itemPrompt = input<string | null>(null);

  readonly openItem = output<string>();

  readonly isOpen = computed(() => !!this.store.review()?.isOpen);
  readonly canReview = computed(() => !!this.store.review()?.canReview);
  readonly canAnswer = computed(() => !!this.store.review()?.canAnswer);

  nameOf(userId: string | null | undefined): string {
    return this.users.displayName(userId);
  }

  answer(note: ReviewNote): void {
    const prompts = this.itemPrompts();
    const prompt = note.checklistItemId ? (prompts?.[note.checklistItemId] ?? this.itemPrompt()) : null;
    this.store.answer(note, prompt);
  }
}
