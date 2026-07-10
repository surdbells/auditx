namespace AuditX.Api.Contracts;

/// <summary>Create a shareable link to a report. <c>ExpiresInDays</c> (1–365) is optional; omit for no expiry.</summary>
public sealed record CreateShareLinkRequest(int? ExpiresInDays);
