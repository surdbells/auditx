using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Integrations;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Integrations.Dtos;
using AuditX.Application.Integrations.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Integrations;
using FluentValidation;

namespace AuditX.Application.Integrations.Commands;

internal static class IntegrationParsing
{
    public static IntegrationType ParseType(string? value)
        => Enum.TryParse<IntegrationType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ConflictException("invalid_integration_type", $"Unknown integration type '{value}'.");
}

public sealed record CreateIntegrationCommand(
    string Type, string Name, string? ConnectionDetailsJson, string? Credentials, int TimeoutSeconds, Guid? FallbackIntegrationId)
    : ICommand<IntegrationDto>;

public sealed class CreateIntegrationCommandValidator : AbstractValidator<CreateIntegrationCommand>
{
    public CreateIntegrationCommandValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateIntegrationCommandHandler(
    IIntegrationRepository integrations,
    ICredentialProtector protector,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateIntegrationCommand, IntegrationDto>
{
    public async Task<IntegrationDto> Handle(CreateIntegrationCommand command, CancellationToken cancellationToken)
    {
        var type = IntegrationParsing.ParseType(command.Type);
        var credentials = string.IsNullOrEmpty(command.Credentials) ? null : protector.Protect(command.Credentials);

        var integration = IntegrationConfiguration.Create(type, command.Name.Trim(), command.ConnectionDetailsJson ?? "{}", credentials, command.TimeoutSeconds);

        if (integration.IsAuthProvider && await integrations.GetActiveAuthProviderAsync(cancellationToken) is { } existing && existing.Id != integration.Id)
        {
            throw new ConflictException("auth_provider_exclusive", $"An authentication provider ({existing.Type}) is already active; deactivate it first.");
        }

        if (command.FallbackIntegrationId is { } fallbackId)
        {
            integration.SetFallback(fallbackId);
        }

        integrations.Add(integration);
        integrations.AddHealth(IntegrationHealthStatus.Create(integration.Id));
        audit.Record(AuditEventTypes.IntegrationConfigured, AuditTargetTypes.Integration, integration.Id,
            after: new { type = integration.Type.ToString(), integration.Name, hasCredentials = credentials is not null });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return integration.ToDto();
    }
}

public sealed record UpdateIntegrationCommand(
    Guid Id, string Name, string? ConnectionDetailsJson, string? Credentials, int TimeoutSeconds, Guid? FallbackIntegrationId, bool IsActive)
    : ICommand<IntegrationDto>;

public sealed class UpdateIntegrationCommandHandler(
    IIntegrationRepository integrations,
    ICredentialProtector protector,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateIntegrationCommand, IntegrationDto>
{
    public async Task<IntegrationDto> Handle(UpdateIntegrationCommand command, CancellationToken cancellationToken)
    {
        var integration = await integrations.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Integration", command.Id);

        var rotated = string.IsNullOrEmpty(command.Credentials) ? null : protector.Protect(command.Credentials);
        integration.UpdateConnection(command.ConnectionDetailsJson ?? integration.ConnectionDetailsJson, rotated, command.TimeoutSeconds);
        integration.SetFallback(command.FallbackIntegrationId);

        if (command.IsActive)
        {
            if (integration.IsAuthProvider && await integrations.GetActiveAuthProviderAsync(cancellationToken) is { } existing && existing.Id != integration.Id)
            {
                throw new ConflictException("auth_provider_exclusive", $"An authentication provider ({existing.Type}) is already active; deactivate it first.");
            }

            integration.Activate();
        }
        else
        {
            integration.Deactivate();
        }

        audit.Record(AuditEventTypes.IntegrationUpdated, AuditTargetTypes.Integration, integration.Id,
            after: new { integration.Name, integration.IsActive, credentialsRotated = rotated is not null });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return integration.ToDto();
    }
}

public sealed record DeactivateIntegrationCommand(Guid Id) : ICommand<Unit>;

public sealed class DeactivateIntegrationCommandHandler(IIntegrationRepository integrations, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateIntegrationCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateIntegrationCommand command, CancellationToken cancellationToken)
    {
        var integration = await integrations.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Integration", command.Id);
        integration.Deactivate();
        audit.Record(AuditEventTypes.IntegrationDeactivated, AuditTargetTypes.Integration, integration.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record TestIntegrationCommand(Guid Id) : ICommand<IntegrationTestResultDto>;

public sealed class TestIntegrationCommandHandler(
    IIntegrationRepository integrations,
    ICredentialProtector protector,
    IIntegrationTester tester,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<TestIntegrationCommand, IntegrationTestResultDto>
{
    public async Task<IntegrationTestResultDto> Handle(TestIntegrationCommand command, CancellationToken cancellationToken)
    {
        var integration = await integrations.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Integration", command.Id);
        var credentials = integration.EncryptedCredentials is { Length: > 0 } c ? protector.Unprotect(c) : null;

        var result = await tester.TestAsync(integration, credentials, cancellationToken);

        var health = await integrations.GetHealthAsync(integration.Id, cancellationToken);
        if (health is not null)
        {
            if (result.Success)
            {
                health.RecordSuccess(clock.UtcNow);
            }
            else
            {
                health.RecordFailure(clock.UtcNow);
            }
        }

        audit.Record(AuditEventTypes.IntegrationTested, AuditTargetTypes.Integration, integration.Id, payload: new { result.Success });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new IntegrationTestResultDto(result.Success, result.Detail);
    }
}
