/**
 * Saved views (D3-A) — a user's named filter/parameter sets per screen, plus shared views.
 *
 * `parametersJson` is an opaque JSON object the owning screen serialises/deserialises into its filter form.
 * Applying a view only pre-fills filter controls; the underlying data stays gated by each screen's own endpoint,
 * so a shared view can never widen access. `isOwner` is resolved for the requesting user (only owners may edit,
 * share or delete).
 */
export interface SavedView {
  id: string;
  ownerUserId: string;
  viewKey: string;
  name: string;
  parametersJson: string;
  isShared: boolean;
  isOwner: boolean;
  version: string;
}

/** Body for POST /saved-views. */
export interface CreateSavedViewRequest {
  viewKey: string;
  name: string;
  parametersJson: string;
  isShared: boolean;
}

/** Body for PATCH /saved-views/{id}. */
export interface UpdateSavedViewRequest {
  name: string;
  parametersJson: string;
  isShared: boolean;
  version: string;
}
