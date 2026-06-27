using AuditX.Domain.Common;
using AuditX.Domain.Sanctions.Events;

namespace AuditX.Domain.Sanctions;

/// <summary>
/// A versioned sanctions grid (M7). A grid maps <c>(category, severity, recurrence)</c> to a recommended sanction
/// range. Exactly one version is active bank-wide (enforced by a filtered unique index on <c>is_active</c>). Creation
/// writes an inactive draft; activation is the policy change worth dual-controlling (maker-checker on activate).
/// </summary>
public sealed class SanctionsGridVersion : AggregateRoot
{
    private SanctionsGridVersion()
    {
    }

    public int VersionNumber { get; private set; }

    /// <summary>JSON: <c>{ "cells": { "&lt;category&gt;|&lt;severity&gt;|&lt;recurrence true|false&gt;": { "recommended_range": "..." } } }</c>.</summary>
    public string GridDefinitionJson { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public string? ActivationReason { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? ActivatedBy { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static SanctionsGridVersion CreateDraft(int versionNumber, string gridDefinitionJson, Guid createdBy, DateTimeOffset nowUtc)
    {
        var grid = new SanctionsGridVersion
        {
            VersionNumber = versionNumber,
            GridDefinitionJson = Guard.NotNullOrWhiteSpace(gridDefinitionJson, "sanctions.grid_definition_required", "A grid definition is required."),
            IsActive = false,
            CreatedByUserId = createdBy,
            CreatedAtUtc = nowUtc,
        };
        grid.RaiseDomainEvent(new GridVersionCreatedEvent(grid.Id, versionNumber, createdBy));
        return grid;
    }

    /// <summary>Activate this version (US-M7-003 requires a ≥20-char reason). The prior active version is deactivated by the caller.</summary>
    public void Activate(string activationReason, Guid activatedBy, DateTimeOffset nowUtc)
    {
        ActivationReason = Guard.MinLength(activationReason, 20, "sanctions.activation_reason_required", "An activation reason of at least 20 characters is required.");
        IsActive = true;
        ActivatedBy = activatedBy;
        ActivatedAt = nowUtc;
        RaiseDomainEvent(new GridVersionActivatedEvent(Id, VersionNumber, activatedBy));
    }

    /// <summary>Deactivate the previously-active version during an atomic switch (keeps the single-active invariant).</summary>
    public void Deactivate() => IsActive = false;
}
