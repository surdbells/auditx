namespace AuditX.Application.Compliance.Dtos;

public sealed record RegulationDto(
    Guid Id,
    string Code,
    string Name,
    string? Authority,
    string? Description,
    string? Category,
    bool IsActive,
    string Version);

public sealed record RegulationListItemDto(
    Guid Id, string Code, string Name, string? Authority, string? Category, bool IsActive);

/// <summary>A control linked to a finding (with the control's code/title for display).</summary>
public sealed record FindingControlLinkDto(Guid LinkId, Guid ControlId, string Code, string Title, DateTimeOffset LinkedAt);

/// <summary>A regulation linked to a finding (with the regulation's code/name for display).</summary>
public sealed record FindingRegulationLinkDto(Guid LinkId, Guid RegulationId, string Code, string Name, DateTimeOffset LinkedAt);

/// <summary>A risk linked to a control (with the risk's title/category/status for display on the control side).</summary>
public sealed record ControlRiskLinkDto(Guid LinkId, Guid RiskId, string Title, string Category, string Status, DateTimeOffset LinkedAt);

/// <summary>One recorded test of a control's effectiveness (append-only history).</summary>
public sealed record ControlTestDto(
    Guid Id, Guid ControlId, Guid? AuditId, Guid? ChecklistItemId, string Result, Guid TestedByUserId, DateTimeOffset TestedAt, string? Notes);
