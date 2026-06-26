/**
 * Generic API envelope used by the AuditX backend.
 * Every successful response wraps the payload in `data`, with optional `metadata`.
 */
export interface ApiResponse<T> {
  data: T;
  metadata?: ApiMetadata | null;
}

export interface ApiMetadata {
  [key: string]: unknown;
}

/** Cursor-paginated collection envelope. */
export interface CursorPage<T> {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
}

/** RFC 7807 problem-details error body returned by the backend. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  request_id?: string;
  error_code?: string;
  field_errors?: FieldError[];
}

export interface FieldError {
  field: string;
  code: string;
  message: string;
}
