import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateWebhookSubscriptionRequest,
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

  listDeliveries(
    status?: WebhookDeliveryStatus | '',
    limit?: number,
  ): Observable<WebhookDelivery[]> {
    return this.api.get<WebhookDelivery[]>('/webhook-deliveries', {
      status,
      limit,
    });
  }

  retryDelivery(id: string): Observable<void> {
    return this.api.postVoid(`/webhook-deliveries/${id}/retry`);
  }
}
