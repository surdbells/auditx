using AuditX.Application.Common.Enums;
using AuditX.Application.Execution.Dtos;
using AuditX.Domain.Audits;
using AuditX.Domain.Enums;

namespace AuditX.Application.Execution.Mapping;

public static class WorkingPaperReviewMappings
{
    public static ReviewNoteDto ToDto(this ReviewNote n) => new(
        n.Id, n.ChecklistItemId, n.Body, n.Status.ToSnake(), n.RaisedBy, n.RaisedAt,
        n.Answer, n.AnsweredBy, n.AnsweredAt, n.ClearedBy, n.ClearedAt);

    /// <summary>
    /// The audit's working-paper review as <paramref name="userId"/> sees it: every item's sign-off, every note, the
    /// totals the completion gate checks, and what this person may do (sign off / raise / clear, or answer).
    /// </summary>
    public static WorkingPaperReviewDto ToReviewDto(this Audit audit, Guid? userId)
    {
        var isOpen = audit.Status is AuditStatus.InProgress or AuditStatus.UnderReview;
        var outstandingByItem = audit.ReviewNotes
            .Where(n => n.IsOutstanding && n.ChecklistItemId is not null)
            .GroupBy(n => n.ChecklistItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var items = audit.ChecklistItems
            .OrderBy(i => i.OrderIndex)
            .Select(i =>
            {
                var response = audit.Responses.FirstOrDefault(r => r.ChecklistItemId == i.Id && !r.IsDraft);
                var answered = i.ItemState == ChecklistItemState.Responded && response is not null;
                return new ReviewItemDto(
                    i.Id, answered, answered ? response!.ResponderUserId : null, i.ReviewedBy, i.ReviewedAt,
                    outstandingByItem.GetValueOrDefault(i.Id));
            })
            .ToArray();

        var onTeam = userId is { } uid && audit.IsOnAuditTeam(uid);
        return new WorkingPaperReviewDto(
            IsRequired: !audit.IsSelfAssessment,
            IsOpen: isOpen,
            CanReview: isOpen && userId is { } reviewer && audit.IsReviewer(reviewer),
            CanAnswer: isOpen && onTeam,
            TotalItems: items.Length,
            SignedOffItems: items.Count(i => i.ReviewedBy is not null),
            ReadyForSignOff: items.Count(i => i.IsAnswered && i.ReviewedBy is null && i.OutstandingNotes == 0),
            OpenNotes: audit.ReviewNotes.Count(n => n.Status == ReviewNoteStatus.Open),
            AnsweredNotes: audit.ReviewNotes.Count(n => n.Status == ReviewNoteStatus.Answered),
            Items: items,
            Notes: audit.ReviewNotes.OrderBy(n => n.RaisedAt).Select(n => n.ToDto()).ToArray(),
            Version: RowVersionToken.Encode(audit.Version));
    }
}
