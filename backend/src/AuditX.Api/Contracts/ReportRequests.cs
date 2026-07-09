namespace AuditX.Api.Contracts;

/// <summary>Body for POST /audits/{id}/reports. HTML is always produced; <c>docx</c> additionally requests DOCX.</summary>
public sealed record GenerateReportRequest(bool? Docx);

/// <summary>
/// Body for POST /reports/standalone. <c>kind</c> is the standalone report kind (<c>executive_summary</c>,
/// <c>annual_plan_status</c>, <c>kpi_pack</c>). HTML is always produced; <c>docx</c> additionally requests DOCX.
/// </summary>
public sealed record GenerateStandaloneReportRequest(string Kind, bool? Docx);

/// <summary>
/// Body for POST /reports/{id}/distribute. Recipients are directory users (by id), role names (expanded to their
/// active members), and/or ad-hoc email addresses. NO distribution-list entity (deferred — A1), NO SMS.
/// </summary>
public sealed record DistributeReportRequest(
    IReadOnlyList<Guid>? RecipientUserIds, IReadOnlyList<string>? RecipientEmailAddresses,
    IReadOnlyList<string>? RecipientRoleNames = null);

public sealed record CreateReportTemplateRequest(string Name, string TemplateDefinition);

public sealed record ActivateReportTemplateRequest(string Reason);
