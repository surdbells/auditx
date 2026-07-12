import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateWebhookSubscriptionRequest,
  PagedResult,
  WebhookDelivery,
  WebhookDeliveryStatus,
  WebhookEventType,
  WebhookSubscription,
} from '../models';

/** Typed client for the M14 Webhook subscription & delivery endpoints. */
@Injectable({ providedIn: 'root' })
export class WebhooksService {
  private readonly api = inject(ApiService);

  /** The catalogue of subscribable event types for the subscription editor dropdown. */
  eventTypes(): Observable<WebhookEventType[]> {
    return this.api.get<WebhookEventType[]>('/webhook-event-types');
  }

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
   * Offset-paginated delivery log. Optional `subscriptionId` / `status` filters
   * and `page` / `pageSize` controls are forwarded as query params.
   */
  listDeliveries(params: {
    subscriptionId?: string;
    status?: WebhookDeliveryStatus | '';
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<WebhookDelivery>> {
    return this.api.get<PagedResult<WebhookDelivery>>('/webhook-deliveries', {
      subscriptionId: params.subscriptionId,
      status: params.status,
      page: params.page,
      pageSize: params.pageSize,
    });
  }

  retryDelivery(id: string): Observable<void> {
    return this.api.postVoid(`/webhook-deliveries/${id}/retry`);
  }
}
