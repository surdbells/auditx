using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Application.Exceptions.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using FluentValidation;

namespace AuditX.Application.Exceptions.Commands;

internal static class ExceptionRaisingRuleParsing
{
    public static ResponseType ParseResponseType(string? value)
        => Enum.TryParse<ResponseType>((value ?? string.Empty).Replace("_", string.Empty), ignoreCase: true, out var r)
            ? r
            : throw new ConflictException("invalid_response_type", $"Unknown response type '{value}'.");
}

public sealed record CreateExceptionRaisingRuleCommand(string ResponseType, bool AllowOnNa, decimal? ScoreThreshold) : ICommand<ExceptionRaisingRuleDto>;

public sealed class CreateExceptionRaisingRuleCommandValidator : AbstractValidator<CreateExceptionRaisingRuleCommand>
{
    public CreateExceptionRaisingRuleCommandValidator() => RuleFor(x => x.ResponseType).NotEmpty();
}

public sealed class CreateExceptionRaisingRuleCommandHandler(IExceptionRaisingRuleRepository rules, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateExceptionRaisingRuleCommand, ExceptionRaisingRuleDto>
{
    public async Task<ExceptionRaisingRuleDto> Handle(CreateExceptionRaisingRuleCommand command, CancellationToken cancellationToken)
    {
        var responseType = ExceptionRaisingRuleParsing.ParseResponseType(command.ResponseType);
        if (await rules.GetByResponseTypeAsync(responseType, cancellationToken) is not null)
        {
            throw new ConflictException("exception_rule.already_exists", $"A rule for response type '{command.ResponseType}' already exists — edit it instead.");
        }

        var rule = ExceptionRaisingRule.Create(responseType, command.AllowOnNa, command.ScoreThreshold);
        rules.Add(rule);
        audit.Record(AuditEventTypes.ExceptionRaisingRuleConfigured, AuditTargetTypes.ExceptionRaisingRule, rule.Id, after: new { rule.ResponseType, rule.AllowOnNa, rule.ScoreThreshold });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return rule.ToDto();
    }
}

public sealed record UpdateExceptionRaisingRuleCommand(Guid Id, bool AllowOnNa, decimal? ScoreThreshold, bool IsActive) : ICommand<ExceptionRaisingRuleDto>;

public sealed class UpdateExceptionRaisingRuleCommandHandler(IExceptionRaisingRuleRepository rules, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateExceptionRaisingRuleCommand, ExceptionRaisingRuleDto>
{
    public async Task<ExceptionRaisingRuleDto> Handle(UpdateExceptionRaisingRuleCommand command, CancellationToken cancellationToken)
    {
        var rule = await rules.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Exception raising rule", command.Id);
        rule.Update(command.AllowOnNa, command.ScoreThreshold, command.IsActive);
        audit.Record(AuditEventTypes.ExceptionRaisingRuleConfigured, AuditTargetTypes.ExceptionRaisingRule, rule.Id, after: new { rule.AllowOnNa, rule.ScoreThreshold, rule.IsActive });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return rule.ToDto();
    }
}

public sealed record ListExceptionRaisingRulesQuery : IQuery<IReadOnlyList<ExceptionRaisingRuleDto>>;

public sealed class ListExceptionRaisingRulesQueryHandler(IExceptionRaisingRuleRepository rules)
    : IQueryHandler<ListExceptionRaisingRulesQuery, IReadOnlyList<ExceptionRaisingRuleDto>>
{
    public async Task<IReadOnlyList<ExceptionRaisingRuleDto>> Handle(ListExceptionRaisingRulesQuery query, CancellationToken cancellationToken)
        => (await rules.GetAllAsync(cancellationToken)).Select(r => r.ToDto()).ToArray();
}
