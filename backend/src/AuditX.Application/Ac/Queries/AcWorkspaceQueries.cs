using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Ac.Commands;
using AuditX.Application.Ac.Dtos;
using AuditX.Application.Ac.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Application.Ac.Queries;

// ---- List AC action items (ACMember/CIA; cursor + ?status) ----

public sealed record ListAcActionItemsQuery(string? Status, string? Cursor, int? Limit) : IQuery<CursorPage<AcActionItemDto>>;

public sealed class ListAcActionItemsQueryHandler(IAcActionItemRepository items, ICurrentUser currentUser)
    : IQueryHandler<ListAcActionItemsQuery, CursorPage<AcActionItemDto>>
{
    public async Task<CursorPage<AcActionItemDto>> Handle(ListAcActionItemsQuery query, CancellationToken cancellationToken)
    {
        _ = currentUser.UserId ?? throw new UnauthorizedException();
        var status = ParseStatus(query.Status);
        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await items.ListAsync(status, page, cancellationToken);
        return new CursorPage<AcActionItemDto>(result.Items.Select(i => i.ToDto()).ToArray(), result.NextCursor, result.HasMore);
    }

    private static AcActionItemStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        foreach (var candidate in Enum.GetValues<AcActionItemStatus>())
        {
            if (string.Equals(candidate.ToString(), status, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Common.Enums.EnumExtensions.ToSnake(candidate), status, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new DomainException("ac_action_item.invalid_status", $"Unknown AC action-item status '{status}'.");
    }
}

// ---- Get AC comments by polymorphic target (ACMember) ----

public sealed record GetAcCommentsQuery(string TargetType, Guid TargetId) : IQuery<IReadOnlyList<AcCommentDto>>;

public sealed class GetAcCommentsQueryHandler(IAcCommentRepository comments, ICurrentUser currentUser)
    : IQueryHandler<GetAcCommentsQuery, IReadOnlyList<AcCommentDto>>
{
    public async Task<IReadOnlyList<AcCommentDto>> Handle(GetAcCommentsQuery query, CancellationToken cancellationToken)
    {
        _ = currentUser.UserId ?? throw new UnauthorizedException();
        var targetType = AddAcCommentCommandHandler.ParseTargetType(query.TargetType);
        var found = await comments.ListByTargetAsync(targetType, query.TargetId, cancellationToken);
        return found.Select(c => c.ToDto()).ToArray();
    }
}
