using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Ac.Generation;
using AuditX.Application.Ac.Mapping;
using AuditX.Domain.Enums;

namespace AuditX.Application.Ac;

/// <summary>
/// Loads the restricted-finding allow-lists (FR-M13-009) for applying the per-requester visibility filter. The
/// allow-lists are loaded LIVE at read (never baked into the immutable pack snapshot) so a restriction added after a
/// pack was sealed still hides detail at read while aggregate counts remain coherent.
/// </summary>
public static class AcVisibility
{
    /// <summary>Shown in place of a restricted finding's title for a requester who is not on its allow-list.</summary>
    public const string RestrictedPlaceholder = "Restricted finding pending chair review";

    /// <summary>
    /// Apply the per-requester restricted-visibility filter to a composition before RENDERING an artefact (the
    /// download path for non-CIA members). Restricted findings the requester is not allow-listed for keep their
    /// row (so aggregate counts stay coherent) but have their title replaced by <see cref="RestrictedPlaceholder"/>.
    /// </summary>
    public static AcPackComposition RedactForRequester(
        AcPackComposition composition, Guid requesterId, IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> exceptionAllowLists)
    {
        if (exceptionAllowLists.Count == 0)
        {
            return composition;
        }

        var findings = composition.MaterialFindings
            .Select(f => exceptionAllowLists.TryGetValue(f.ExceptionId, out var allowed) && !allowed.Contains(requesterId)
                ? f with { Title = RestrictedPlaceholder }
                : f)
            .ToArray();

        return composition with { MaterialFindings = findings };
    }

    /// <summary>
    /// Build a map of (exception id → allowed user-id set) for every restricted exception finding. An exception
    /// absent from the map is unrestricted (visible to all); a present exception is visible only to the listed users.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> LoadExceptionAllowListsAsync(
        IFindingVisibilityRestrictionRepository restrictions, CancellationToken cancellationToken)
    {
        var all = await restrictions.ListByTypeAsync(FindingType.Exception, cancellationToken);
        var map = new Dictionary<Guid, IReadOnlySet<Guid>>();
        foreach (var restriction in all)
        {
            map[restriction.FindingId] = AcMappings.ParseUserIds(restriction.AllowedUserIdsJson).ToHashSet();
        }

        return map;
    }
}
