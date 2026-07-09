/**
 * Global (cross-module) header search. The backend returns a flat list of hits
 * discriminated by `type`; the header component groups + routes them.
 */

export type SearchHitType =
  | 'audit'
  | 'plan'
  | 'exception'
  | 'template'
  | 'user';

export interface SearchHit {
  type: SearchHitType | string;
  id: string;
  title: string;
  subtitle: string | null;
}

export interface GlobalSearchResults {
  hits: SearchHit[];
}
