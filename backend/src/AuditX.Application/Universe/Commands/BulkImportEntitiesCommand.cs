using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Universe;
using AuditX.Application.Common.Csv;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Universe.Dtos;
using AuditX.Application.Universe.Services;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Universe;

namespace AuditX.Application.Universe.Commands;

/// <summary>
/// Atomic CSV bulk import of universe entities (US-M3-006). Columns: name, entity_type, parent_name,
/// owner_email, description — <c>name</c> and <c>entity_type</c> are required, the rest optional. Header names are
/// matched leniently (case, and "entityType" / "entity type" / "type" for entity_type, and likewise for the others),
/// because the same sheet gets retyped by hand. All-or-none: if any row has an error, nothing is persisted and the
/// full error list is returned. Parent resolution prefers names within the batch, then existing entities;
/// acyclicity is validated in-memory over the combined candidate graph.
/// </summary>
public sealed record BulkImportEntitiesCommand(string CsvContent) : ICommand<BulkImportResultDto>;

public sealed class BulkImportEntitiesCommandHandler(
    IAuditUniverseRepository entities,
    IUserRepository users,
    ITaxonomyProvider taxonomy,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<BulkImportEntitiesCommand, BulkImportResultDto>
{
    public async Task<BulkImportResultDto> Handle(BulkImportEntitiesCommand command, CancellationToken cancellationToken)
    {
        var errors = new List<BulkImportErrorDto>();

        // Fail fast, once, on a sheet that cannot work — instead of one confusing error per row.
        var headers = CsvReader.Headers(command.CsvContent).Select(Canonical).ToHashSet(StringComparer.Ordinal);
        foreach (var required in new[] { "name", "entity_type" })
        {
            if (!headers.Contains(required))
            {
                errors.Add(new BulkImportErrorDto(1, required,
                    $"The header row has no '{required}' column. The first line must be: name,entity_type,parent_name,owner_email,description"));
            }
        }

        if (errors.Count > 0)
        {
            return new BulkImportResultDto(0, errors);
        }

        var rows = CsvReader.Parse(command.CsvContent)
            .Select(r => (IReadOnlyDictionary<string, string>)r.GroupBy(kv => Canonical(kv.Key)).ToDictionary(g => g.Key, g => g.First().Value))
            .ToArray();
        if (rows.Length == 0)
        {
            return new BulkImportResultDto(0, [new BulkImportErrorDto(1, "name", "The sheet has a header row but no entities below it.")]);
        }

        var activeTypes = (await taxonomy.GetActiveEntityTypesAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = rows.Select(r => Get(r, "owner_email")).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var emailToUser = await users.GetIdsByEmailsAsync(emails, cancellationToken);

        var parentNames = rows.Select(r => Get(r, "parent_name")).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = await entities.GetByNamesAsync(parentNames, cancellationToken);
        var existingByName = existing.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Stage entities (Id assigned on construction) so in-batch parents resolve by name.
        var staged = new List<(int Row, string Name, string? ParentName, AuditableEntity Entity)>();
        var batchByName = new Dictionary<string, List<AuditableEntity>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Length; i++)
        {
            var rowNo = i + 2; // header is row 1
            var row = rows[i];
            var name = Get(row, "name");
            var entityType = Get(row, "entity_type");
            var ownerEmail = Get(row, "owner_email");
            var description = Get(row, "description");
            var parentName = Get(row, "parent_name");

            if (name.Length == 0)
            {
                errors.Add(new BulkImportErrorDto(rowNo, "name", "Name is required."));
                continue;
            }

            if (!activeTypes.Contains(entityType))
            {
                errors.Add(new BulkImportErrorDto(rowNo, "entity_type", entityType.Length == 0
                    ? "Entity type is required."
                    : $"Unknown entity type '{entityType}'. Use one of: {string.Join(", ", activeTypes.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))}."));
                continue;
            }

            Guid? ownerId = null;
            if (ownerEmail.Length > 0)
            {
                if (!emailToUser.TryGetValue(ownerEmail, out var resolved))
                {
                    errors.Add(new BulkImportErrorDto(rowNo, "owner_email", $"No user found for '{ownerEmail}'."));
                    continue;
                }

                ownerId = resolved;
            }

            var entity = AuditableEntity.Create(entityType, name, description.Length == 0 ? null : description, parentEntityId: null, ownerId);
            staged.Add((rowNo, name, parentName.Length == 0 ? null : parentName, entity));
            if (!batchByName.TryGetValue(name, out var list))
            {
                batchByName[name] = list = [];
            }

            list.Add(entity);
        }

        // Resolve parents (batch first, then existing), detecting ambiguity.
        var parentMap = new Dictionary<Guid, Guid?>();
        foreach (var (rowNo, _, parentName, entity) in staged)
        {
            Guid? parentId = null;
            if (parentName is not null)
            {
                var inBatch = batchByName.TryGetValue(parentName, out var batchMatches) ? batchMatches : [];
                var inExisting = existingByName.TryGetValue(parentName, out var existingMatches) ? existingMatches : [];
                var total = inBatch.Count + inExisting.Count;
                if (total == 0)
                {
                    errors.Add(new BulkImportErrorDto(rowNo, "parent_name", $"Parent '{parentName}' not found."));
                }
                else if (total > 1)
                {
                    errors.Add(new BulkImportErrorDto(rowNo, "parent_name", $"Parent '{parentName}' is ambiguous."));
                }
                else
                {
                    parentId = inBatch.Count == 1 ? inBatch[0].Id : inExisting[0].Id;
                    entity.SetParent(parentId);
                }
            }

            parentMap[entity.Id] = parentId;
        }

        // Merge existing universe graph for full-graph cycle detection.
        foreach (var (id, pid) in await entities.GetParentMapAsync(cancellationToken))
        {
            parentMap.TryAdd(id, pid);
        }

        foreach (var (rowNo, _, _, entity) in staged)
        {
            if (entity.ParentEntityId is { } pid && HierarchyGuard.WouldCreateCycle(parentMap, entity.Id, pid))
            {
                errors.Add(new BulkImportErrorDto(rowNo, "parent_name", "Import would create a hierarchy cycle."));
            }
        }

        if (errors.Count > 0)
        {
            return new BulkImportResultDto(0, errors.OrderBy(e => e.Row).ToArray());
        }

        entities.AddRange(staged.Select(s => s.Entity));
        audit.Record(AuditEventTypes.EntityBulkImported, AuditTargetTypes.AuditUniverseEntity, null, payload: new { count = staged.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new BulkImportResultDto(staged.Count, []);
    }

    private static string Get(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var v) ? v.Trim() : string.Empty;

    /// <summary>
    /// Maps a header as typed to the column it means: case, spaces, dashes and underscores are ignored, and the
    /// obvious short forms are accepted ("entityType", "Entity Type", "type" → entity_type).
    /// </summary>
    private static string Canonical(string header)
        => new string(header.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant() switch
        {
            "name" or "entityname" => "name",
            "entitytype" or "type" => "entity_type",
            "parentname" or "parent" or "parententity" => "parent_name",
            "owneremail" or "owner" or "email" => "owner_email",
            "description" or "desc" => "description",
            var other => other,
        };
}
