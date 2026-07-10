/**
 * Shareable links (D3-B) — an opaque, revocable, optionally-expiring reference to a report.
 *
 * SECURITY: a link is a reference, never an access grant. Resolving it returns only the target descriptor (no
 * content); opening the target still passes that resource's own permission check. So a link never widens access.
 */
export type SharedLinkTargetType = 'report';

export interface SharedLink {
  id: string;
  slug: string;
  targetType: SharedLinkTargetType;
  targetId: string;
  createdByUserId: string;
  createdAt: string;
  expiresAt: string | null;
  revokedAt: string | null;
  isActive: boolean;
  version: string;
}

/** The minimal descriptor a resolved slug returns. */
export interface SharedLinkTarget {
  targetType: string;
  targetId: string;
}
