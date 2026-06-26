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
/// owner_email, description. All-or-none: if any row has an error, nothing is persisted and the full
/// error list is returned. Parent resolution prefers names within the batch, then existing entities;
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
        var rows = CsvReader.Parse(command.CsvContent);
        var errors = new List<BulkImportErrorDto>();

        var activeTypes = (await taxonomy.GetActiveEntityTypesAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = rows.Select(r => Get(r, "owner_email")).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var emailToUser = await users.GetIdsByEmailsAsync(emails, cancellationToken);

        var parentNames = rows.Select(r => Get(r, "parent_name")).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = await entities.GetByNamesAsync(parentNames, cancellationToken);
        var existingByName = existing.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Stage entities (Id assigned on construction) so in-batch parents resolve by name.
        var staged = new List<(int Row, string Name, string? ParentName, AuditableEntity Entity)>();
        var batchByName = new Dictionary<string, List<AuditableEntity>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
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
                errors.Add(new BulkImportErrorDto(rowNo, "entity_type", $"Unknown entity type '{entityType}'."));
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
}
