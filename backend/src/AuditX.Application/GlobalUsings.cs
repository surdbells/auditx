// Rowversion optimistic-concurrency token helper (RowVersionToken) is used pervasively by DTO mappings and the
// per-aggregate concurrency guards; a global using keeps those call sites terse without a per-file import.
global using AuditX.Application.Common.Concurrency;
