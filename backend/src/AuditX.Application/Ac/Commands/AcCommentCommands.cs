using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Ac.Dtos;
using AuditX.Application.Ac.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Ac;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using FluentValidation;

namespace AuditX.Application.Ac.Commands;

// ---- Add a polymorphic AC comment (ACMember) ----

public sealed record AddAcCommentCommand(string TargetType, Guid TargetId, string Comment) : ICommand<AcCommentDto>;

public sealed class AddAcCommentCommandValidator : AbstractValidator<AddAcCommentCommand>
{
    public AddAcCommentCommandValidator()
    {
        RuleFor(x => x.TargetType).NotEmpty();
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(10000);
    }
}

public sealed class AddAcCommentCommandHandler(
    IAcCommentRepository comments, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<AddAcCommentCommand, AcCommentDto>
{
    public async Task<AcCommentDto> Handle(AddAcCommentCommand command, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedException();
        var targetType = ParseTargetType(command.TargetType);

        var comment = AcComment.Create(targetType, command.TargetId, command.Comment, actorId, clock.UtcNow);
        comments.Add(comment);

        audit.Record(AuditEventTypes.AcCommentAdded, AuditTargetTypes.AcComment, comment.Id,
            after: new { targetType = targetType.ToString().ToLowerInvariant(), command.TargetId });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return comment.ToDto();
    }

    internal static AcCommentTargetType ParseTargetType(string value)
    {
        foreach (var candidate in Enum.GetValues<AcCommentTargetType>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new DomainException("ac_comment.invalid_target_type", "The comment target type must be one of plan, pack or finding.");
    }
}
