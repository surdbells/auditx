import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateWebhookSubscriptionRequest,
  CursorPage,
  WebhookDelivery,
  WebhookDeliveryStatus,
  WebhookSubscription,
} from '../models';

/** Typed client for the M14 Webhook subscription & delivery endpoints. */
@Injectable({ providedIn: 'root' })
export class WebhooksService {
  private readonly api = inject(ApiService);

  listSubscriptions(): Observable<WebhookSubscription[]> {
    return this.api.get<WebhookSubscription[]>('/webhook-subscriptions');
  }

  createSubscription(
    body: CreateWebhookSubscriptionRequest,
  ): Observable<WebhookSubscription> {
    return this.api.post<WebhookSubscription>('/webhook-subscriptions', body);
  }

  deleteSubscription(id: string): Observable<void> {
    return this.api.deleteVoid(`/webhook-subscriptions/${id}`);
  }

  /**
   * Cursor-paginated delivery log. Optional `subscriptionId` / `status` filters
   * and `cursor` / `limit` page controls are forwarded as query params.
   */
  listDeliveries(params: {
    subscriptionId?: string;
    status?: WebhookDeliveryStatus | '';
    cursor?: string;
    limit?: number;
  }): Observable<CursorPage<WebhookDelivery>> {
    return this.api.get<CursorPage<WebhookDelivery>>('/webhook-deliveries', {
      subscriptionId: params.subscriptionId,
      status: params.status,
      cursor: params.cursor,
      limit: params.limit,
    });
  }

  retryDelivery(id: string): Observable<void> {
    return this.api.postVoid(`/webhook-deliveries/${id}/retry`);
  }
}
