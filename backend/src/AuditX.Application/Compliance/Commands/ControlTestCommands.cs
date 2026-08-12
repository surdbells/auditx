using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Compliance.Dtos;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Controls;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Compliance.Commands;

internal static class ControlTestParsing
{
    public static ControlEffectiveness ParseResult(string? value)
        => Enum.TryParse<ControlEffectiveness>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var r)
            ? r
            : throw new ConflictException("control_test.invalid_result", $"Unknown control-effectiveness result '{value}'.");

    /// <summary>The effectiveness a checklist-item response suggests, so the UI/manager can accept or override it.</summary>
    public static ControlEffectiveness SuggestFromResponse(ResponseVerdict? verdict, decimal? score)
    {
        // A finalised Pass reads as Effective, a Fail as Ineffective. A scored (Rating) response with no verdict
        // maps by band: >=75 Effective, >=50 Partially Effective, else Ineffective.
        if (verdict == ResponseVerdict.Pass)
        {
            return ControlEffectiveness.Effective;
        }
        if (verdict == ResponseVerdict.Fail)
        {
            return ControlEffectiveness.Ineffective;
        }
        if (score is { } s)
        {
            return s >= 75 ? ControlEffectiveness.Effective
                : s >= 50 ? ControlEffectiveness.PartiallyEffective
                : ControlEffectiveness.Ineffective;
        }

        return ControlEffectiveness.PartiallyEffective;
    }
}

/// <summary>
/// Record a test of a control's effectiveness. When <paramref name="AuditId"/>/<paramref name="ChecklistItemId"/>
/// are supplied, the item must test THIS control and carry a finalised response (the audit fieldwork IS the
/// evidence); an omitted <paramref name="Result"/> is then derived from that response. An ad-hoc test (no audit)
/// must supply an explicit result. Records an append-only ControlTest and updates the control's current
/// effectiveness + last-tested date.
/// </summary>
public sealed record RecordControlTestCommand(Guid ControlId, Guid? AuditId, Guid? ChecklistItemId, string? Result, string? Notes) : ICommand<ControlTestDto>;

public sealed class RecordControlTestCommandValidator : AbstractValidator<RecordControlTestCommand>
{
    public RecordControlTestCommandValidator() => RuleFor(x => x.ControlId).NotEmpty();
}

public sealed class RecordControlTestCommandHandler(
    IControlRepository controls, IControlTestRepository tests, IAuditRepository audits,
    ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordControlTestCommand, ControlTestDto>
{
    public async Task<ControlTestDto> Handle(RecordControlTestCommand command, CancellationToken cancellationToken)
    {
        var control = await controls.GetByIdAsync(command.ControlId, cancellationToken) ?? throw new NotFoundException("Control", command.ControlId);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        ControlEffectiveness result;
        if (command.AuditId is { } auditId && command.ChecklistItemId is { } itemId)
        {
            // Audit-driven test: the item must exist, test THIS control, and have a finalised response.
            var auditEntity = await audits.GetByIdAsync(auditId, cancellationToken) ?? throw new NotFoundException("Audit", auditId);
            var item = auditEntity.ChecklistItems.FirstOrDefault(i => i.Id == itemId)
                ?? throw new NotFoundException("Checklist item", itemId);
            if (item.ControlId != command.ControlId)
            {
                throw new ConflictException("control_test.item_not_for_control", "This checklist item does not test the specified control.");
            }

            var response = auditEntity.Responses.FirstOrDefault(r => r.ChecklistItemId == itemId && !r.IsDraft)
                ?? throw new ConflictException("control_test.no_response", "The checklist item has no finalised response to test the control from.");

            // An explicit result overrides the suggestion (professional judgment); otherwise derive it.
            result = string.IsNullOrWhiteSpace(command.Result)
                ? ControlTestParsing.SuggestFromResponse(response.Verdict, response.Score)
                : ControlTestParsing.ParseResult(command.Result);
        }
        else if (command.AuditId is not null || command.ChecklistItemId is not null)
        {
            throw new ConflictException("control_test.audit_item_required_together", "An audit id and checklist item id must be supplied together.");
        }
        else
        {
            // Ad-hoc test outside an audit — an explicit result is required.
            result = string.IsNullOrWhiteSpace(command.Result)
                ? throw new ConflictException("control_test.result_required", "An ad-hoc control test must specify a result.")
                : ControlTestParsing.ParseResult(command.Result);
        }

        var now = clock.UtcNow;
        var test = ControlTest.Create(command.ControlId, command.AuditId, command.ChecklistItemId, result, userId, now, command.Notes);
        tests.Add(test);
        control.RecordTest(result, DateOnly.FromDateTime(now.UtcDateTime));
        audit.Record(AuditEventTypes.ControlTested, AuditTargetTypes.Control, command.ControlId,
            after: new { result = result.ToSnake(), command.AuditId, command.ChecklistItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ControlTestDto(test.Id, test.ControlId, test.AuditId, test.ChecklistItemId, test.Result.ToSnake(), test.TestedByUserId, test.TestedAt, test.Notes);
    }
}

public sealed record ListControlTestsQuery(Guid ControlId) : IQuery<IReadOnlyList<ControlTestDto>>;

public sealed class ListControlTestsQueryHandler(IControlTestRepository tests)
    : IQueryHandler<ListControlTestsQuery, IReadOnlyList<ControlTestDto>>
{
    public async Task<IReadOnlyList<ControlTestDto>> Handle(ListControlTestsQuery query, CancellationToken cancellationToken)
    {
        var rows = await tests.ListForControlAsync(query.ControlId, cancellationToken);
        return rows.Select(t => new ControlTestDto(t.Id, t.ControlId, t.AuditId, t.ChecklistItemId, t.Result.ToSnake(), t.TestedByUserId, t.TestedAt, t.Notes)).ToArray();
    }
}
