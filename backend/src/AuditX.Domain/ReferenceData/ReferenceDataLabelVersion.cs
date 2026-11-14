using AuditX.Domain.Common;

namespace AuditX.Domain.ReferenceData;

/// <summary>
/// One label a reference-data value has carried, and from when. Renaming a value appends a version instead of
/// overwriting history, so a record keeps showing the label that was current when it was created — renaming the
/// risk category "Fraud" to "Fraudulent" changes how NEW records read, not how last year's audits read.
/// </summary>
public sealed class ReferenceDataLabelVersion : Entity, IBelongsToAggregate
{
    private ReferenceDataLabelVersion()
    {
    }

    internal ReferenceDataLabelVersion(Guid itemId, string label, DateTimeOffset effectiveFrom)
    {
        ItemId = itemId;
        Label = label;
        EffectiveFrom = effectiveFrom;
    }

    public Guid ItemId { get; private set; }

    public string Label { get; private set; } = null!;

    /// <summary>From when this label applies. The original label applies from <see cref="DateTimeOffset.MinValue"/>.</summary>
    public DateTimeOffset EffectiveFrom { get; private set; }

    public Guid AggregateRootId => ItemId;
}
