using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Administration;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Administration.Dtos;
using AuditX.Application.Administration.Mapping;
using AuditX.Application.Common.Csv;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Administration;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using FluentValidation;

namespace AuditX.Application.Administration.Commands;

// ---- Bank settings & limits ----

public sealed record UpdateBankSettingsCommand(
    string BankDisplayName, string Timezone, string LocaleDefault, string? AdProvisioningFilterOuDn, string? AdProvisioningFilterGroupSid,
    bool AllowOverlappingPlanPeriods, bool AllowAuditLaunchBeforeApproval,
    string PrimaryColor, string AccentColor, string? LogoDataUri, string? IconDataUri,
    bool ShowOverview, bool ShowWalkthrough)
    : ICommand<BankSettingsDto>;

public sealed class UpdateBankSettingsCommandValidator : AbstractValidator<UpdateBankSettingsCommand>
{
    // ~512 KB decoded ⇒ base64 is ~4/3 of that; a generous cap keeps a logo/icon inline without bloating the row.
    private const int MaxBrandingAssetChars = 700_000;

    public UpdateBankSettingsCommandValidator()
    {
        RuleFor(x => x.BankDisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Timezone).NotEmpty();
        RuleFor(x => x.LocaleDefault).NotEmpty();
        RuleFor(x => x.PrimaryColor).NotEmpty().Matches("^#[0-9a-fA-F]{6}$")
            .WithMessage("Primary colour must be a 6-digit hex value like #4f46e5.");
        RuleFor(x => x.AccentColor).NotEmpty().Matches("^#[0-9a-fA-F]{6}$")
            .WithMessage("Accent colour must be a 6-digit hex value like #7c3aed.");
        RuleFor(x => x.LogoDataUri).Must(BeValidImageDataUri).When(x => !string.IsNullOrWhiteSpace(x.LogoDataUri))
            .WithMessage("Logo must be a data:image/* URI under 512 KB.");
        RuleFor(x => x.IconDataUri).Must(BeValidImageDataUri).When(x => !string.IsNullOrWhiteSpace(x.IconDataUri))
            .WithMessage("Icon must be a data:image/* URI under 512 KB.");
    }

    private static bool BeValidImageDataUri(string? value)
        => string.IsNullOrWhiteSpace(value)
           || (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && value.Length <= MaxBrandingAssetChars);
}

public sealed class UpdateBankSettingsCommandHandler(IBankSettingsRepository settings, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateBankSettingsCommand, BankSettingsDto>
{
    public async Task<BankSettingsDto> Handle(UpdateBankSettingsCommand command, CancellationToken cancellationToken)
    {
        var bank = await settings.GetAsync(cancellationToken);
        bank.Update(command.BankDisplayName, command.Timezone, command.LocaleDefault);
        bank.SetAdProvisioningFilter(command.AdProvisioningFilterOuDn, command.AdProvisioningFilterGroupSid);
        bank.SetAllowOverlappingPlanPeriods(command.AllowOverlappingPlanPeriods);
        bank.SetAllowAuditLaunchBeforeApproval(command.AllowAuditLaunchBeforeApproval);
        bank.SetPageGuideVisibility(command.ShowOverview, command.ShowWalkthrough);
        bank.SetBranding(command.PrimaryColor, command.AccentColor, command.LogoDataUri, command.IconDataUri);
        // Keep the audit payload metadata-only — the logo/icon data URIs are deliberately excluded.
        audit.Record(AuditEventTypes.BankSettingsUpdated, AuditTargetTypes.BankSettings, bank.Id, after: new
        {
            bank.BankDisplayName, bank.Timezone, bank.AllowOverlappingPlanPeriods, bank.AllowAuditLaunchBeforeApproval,
            bank.ShowOverview, bank.ShowWalkthrough,
            bank.PrimaryColor, bank.AccentColor, hasLogo = bank.LogoDataUri is not null, hasIcon = bank.IconDataUri is not null,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return bank.ToDto();
    }
}

public sealed record UpdateResourceLimitsCommand(int MaxEvidenceFileMb, int MaxAuditEvidenceGb) : ICommand<ResourceLimitsDto>;

public sealed class UpdateResourceLimitsCommandValidator : AbstractValidator<UpdateResourceLimitsCommand>
{
    // Mirror the domain's accepted range (BankSettings.SetResourceLimits keeps 1..1024) so out-of-range caps
    // fail fast with a 422 instead of being silently ignored.
    public UpdateResourceLimitsCommandValidator()
    {
        RuleFor(x => x.MaxEvidenceFileMb).InclusiveBetween(1, 1024);
        RuleFor(x => x.MaxAuditEvidenceGb).InclusiveBetween(1, 1024);
    }
}

public sealed class UpdateResourceLimitsCommandHandler(IBankSettingsRepository settings, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateResourceLimitsCommand, ResourceLimitsDto>
{
    public async Task<ResourceLimitsDto> Handle(UpdateResourceLimitsCommand command, CancellationToken cancellationToken)
    {
        var bank = await settings.GetAsync(cancellationToken);
        bank.SetResourceLimits(command.MaxEvidenceFileMb, command.MaxAuditEvidenceGb);
        audit.Record(AuditEventTypes.ResourceLimitsUpdated, AuditTargetTypes.BankSettings, bank.Id,
            after: new { bank.MaxEvidenceFileMb, bank.MaxAuditEvidenceGb });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ResourceLimitsDto(bank.MaxEvidenceFileMb, bank.MaxAuditEvidenceGb);
    }
}

// ---- Bulk user operations ----

public sealed record BulkDeactivateUsersCommand(IReadOnlyList<Guid> UserIds) : ICommand<BulkOperationResultDto>;

public sealed class BulkDeactivateUsersCommandHandler(
    IUserRepository users, ICurrentUser currentUser, IPermissionResolver permissions, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<BulkDeactivateUsersCommand, BulkOperationResultDto>
{
    public async Task<BulkOperationResultDto> Handle(BulkDeactivateUsersCommand command, CancellationToken cancellationToken)
    {
        var ids = command.UserIds.Distinct().ToArray();
        var found = await users.GetByIdsAsync(ids, cancellationToken);
        var foundIds = found.Select(u => u.Id).ToHashSet();

        var errors = ids.Where(id => !foundIds.Contains(id))
            .Select(id => new BulkOperationErrorDto(id.ToString(), "User not found."))
            .ToList();

        // Atomic: if any row is invalid, commit nothing (US-M15-002).
        if (errors.Count > 0)
        {
            return new BulkOperationResultDto(0, errors);
        }

        foreach (var user in found)
        {
            user.Deactivate(currentUser.UserId);
        }

        audit.Record(AuditEventTypes.UsersBulkDeactivated, AuditTargetTypes.User, null, payload: new { count = found.Count, ids });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var id in foundIds)
        {
            await permissions.InvalidateAsync(id, cancellationToken);
        }

        return new BulkOperationResultDto(found.Count, []);
    }
}

public sealed record BulkImportUsersCommand(string CsvContent) : ICommand<BulkOperationResultDto>;

public sealed class BulkImportUsersCommandValidator : AbstractValidator<BulkImportUsersCommand>
{
    // Bound the uploaded CSV payload (≈1 MB) so an unbounded body cannot be used to exhaust memory/storage.
    public BulkImportUsersCommandValidator()
    {
        RuleFor(x => x.CsvContent).NotEmpty().MaximumLength(1_000_000);
    }
}

public sealed class BulkImportUsersCommandHandler(
    IUserRepository users, IRoleRepository roles, IUserRoleRepository userRoles, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<BulkImportUsersCommand, BulkOperationResultDto>
{
    public async Task<BulkOperationResultDto> Handle(BulkImportUsersCommand command, CancellationToken cancellationToken)
    {
        var rows = CsvReader.Parse(command.CsvContent);
        var errors = new List<BulkOperationErrorDto>();
        var staged = new List<(User User, IReadOnlyList<string> Roles)>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var identifier = $"row {i + 2}";
            var email = Get(row, "email");
            var firstName = Get(row, "first_name");
            var lastName = Get(row, "last_name");
            var externalId = Get(row, "external_subject_id");

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(externalId))
            {
                errors.Add(new BulkOperationErrorDto(identifier, "email and external_subject_id are required."));
                continue;
            }

            if (await users.ExistsByObjectSidAsync(externalId, cancellationToken))
            {
                errors.Add(new BulkOperationErrorDto(identifier, $"A user with external id '{externalId}' already exists."));
                continue;
            }

            var roleNames = Get(row, "roles").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            try
            {
                var user = User.ProvisionFromDirectory(email, email, externalId, email, firstName, lastName);
                staged.Add((user, roleNames));
            }
            catch (Domain.Common.DomainException ex)
            {
                errors.Add(new BulkOperationErrorDto(identifier, ex.Message));
            }
        }

        if (errors.Count > 0)
        {
            return new BulkOperationResultDto(0, errors);
        }

        foreach (var (user, roleNames) in staged)
        {
            users.Add(user);
            foreach (var roleName in roleNames)
            {
                var role = await roles.GetByNameAsync(roleName, cancellationToken)
                    ?? throw new ConflictException("unknown_role", $"Role '{roleName}' does not exist.");
                userRoles.Add(UserRole.Grant(user.Id, role.Id));
                user.MarkActiveOnFirstRole();
            }
        }

        audit.Record(AuditEventTypes.UsersBulkImported, AuditTargetTypes.User, null, payload: new { count = staged.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new BulkOperationResultDto(staged.Count, []);
    }

    private static string Get(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var v) ? v : string.Empty;
}

// ---- ITANDT support channel ----

public sealed record EnableSupportChannelCommand(IReadOnlyList<string> EngineerIdentifiers, int DurationMinutes) : ICommand<SupportChannelStatusDto>;

public sealed class EnableSupportChannelCommandValidator : AbstractValidator<EnableSupportChannelCommand>
{
    // At least one named engineer; cap the list and each identifier; bound the duration to the domain max
    // (SupportChannelSession.MaxDurationMinutes = 480) so out-of-range values fail fast rather than silently clamp.
    public EnableSupportChannelCommandValidator()
    {
        RuleFor(x => x.EngineerIdentifiers).NotEmpty();
        RuleForEach(x => x.EngineerIdentifiers).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(1, SupportChannelSession.MaxDurationMinutes);
    }
}

public sealed class EnableSupportChannelCommandHandler(
    IAdministrationRepository admin, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<EnableSupportChannelCommand, SupportChannelStatusDto>
{
    public async Task<SupportChannelStatusDto> Handle(EnableSupportChannelCommand command, CancellationToken cancellationToken)
    {
        var enabledBy = currentUser.UserId ?? throw new UnauthorizedException();
        var session = SupportChannelSession.Enable(command.EngineerIdentifiers, enabledBy, clock.UtcNow, command.DurationMinutes);
        admin.AddSupportSession(session);
        audit.Record(AuditEventTypes.SupportChannelEnabled, AuditTargetTypes.SupportChannel, session.Id,
            after: new { engineers = session.EngineerIdentifiers, session.ExpiresAt });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.ToStatusDto(clock.UtcNow);
    }
}

public sealed record RevokeSupportChannelCommand : ICommand<Unit>;

public sealed class RevokeSupportChannelCommandHandler(
    IAdministrationRepository admin, ICurrentUser currentUser, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeSupportChannelCommand, Unit>
{
    public async Task<Unit> Handle(RevokeSupportChannelCommand command, CancellationToken cancellationToken)
    {
        var session = await admin.GetActiveSupportSessionAsync(clock.UtcNow, cancellationToken)
            ?? throw new NotFoundException("Active support session", "none");
        session.Revoke(currentUser.UserId ?? throw new UnauthorizedException(), clock.UtcNow);
        audit.Record(AuditEventTypes.SupportChannelRevoked, AuditTargetTypes.SupportChannel, session.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ---- Signed offline releases ----

public sealed record InstallReleaseCommand(
    string Version, string ManifestSha256, string ChangeRecordReference, string SignatureBase64, string ManifestContentBase64)
    : ICommand<ReleaseInstallDto>;

public sealed class InstallReleaseCommandHandler(
    IReleasePackageVerifier verifier, IAdministrationRepository admin, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<InstallReleaseCommand, ReleaseInstallDto>
{
    public async Task<ReleaseInstallDto> Handle(InstallReleaseCommand command, CancellationToken cancellationToken)
    {
        byte[] signature;
        byte[] manifest;
        try
        {
            signature = Convert.FromBase64String(command.SignatureBase64);
            manifest = Convert.FromBase64String(command.ManifestContentBase64);
        }
        catch (FormatException)
        {
            throw new ConflictException("release_bad_encoding", "Signature and manifest content must be base64-encoded.");
        }

        var verification = verifier.Verify(command.Version, command.ManifestSha256, signature, manifest);
        var status = verification.IsValid ? ReleaseInstallStatus.Installed : ReleaseInstallStatus.Rejected;
        var record = ReleaseInstall.Record(command.Version, command.ManifestSha256, command.ChangeRecordReference, status, verification.Detail);
        admin.AddRelease(record);
        audit.Record(verification.IsValid ? AuditEventTypes.ReleaseInstalled : AuditEventTypes.ReleaseRejected,
            AuditTargetTypes.Release, record.Id, after: new { record.Version, status = status.ToString(), verification.Detail });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!verification.IsValid)
        {
            throw new ConflictException("release_verification_failed", verification.Detail);
        }

        return record.ToDto();
    }
}

// ---- Backup / restore ----

public sealed record RecordRestoreDrillCommand(string Outcome, string? Details) : ICommand<RestoreDrillDto>;

public sealed class RecordRestoreDrillCommandValidator : AbstractValidator<RecordRestoreDrillCommand>
{
    // Outcome is parsed to an enum in the handler; bound the free-text Details to the column length (2000).
    public RecordRestoreDrillCommandValidator()
    {
        RuleFor(x => x.Outcome).NotEmpty();
        RuleFor(x => x.Details).MaximumLength(2000);
    }
}

public sealed class RecordRestoreDrillCommandHandler(IAdministrationRepository admin, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordRestoreDrillCommand, RestoreDrillDto>
{
    public async Task<RestoreDrillDto> Handle(RecordRestoreDrillCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RestoreOutcome>(command.Outcome, ignoreCase: true, out var outcome))
        {
            throw new ConflictException("invalid_outcome", $"Unknown restore outcome '{command.Outcome}'.");
        }

        var drill = RestoreDrill.Record(clock.UtcNow, outcome, command.Details);
        admin.AddRestoreDrill(drill);
        audit.Record(AuditEventTypes.RestoreDrillRecorded, AuditTargetTypes.RestoreDrill, drill.Id, after: new { outcome = outcome.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return drill.ToDto();
    }
}

public sealed record RequestObjectRestoreCommand(string ObjectType, Guid ObjectId, DateTimeOffset SnapshotDate, string Justification)
    : ICommand<ObjectRestoreRequestDto>;

public sealed class RequestObjectRestoreCommandValidator : AbstractValidator<RequestObjectRestoreCommand>
{
    // Bound the free-text fields to their column lengths (object_type 100, justification 2000) so an oversized
    // body fails fast with a 422 rather than reaching the DB.
    public RequestObjectRestoreCommandValidator()
    {
        RuleFor(x => x.ObjectType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Justification).NotEmpty().MaximumLength(2000);
    }
}

public sealed class RequestObjectRestoreCommandHandler(
    IAdministrationRepository admin, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RequestObjectRestoreCommand, ObjectRestoreRequestDto>
{
    public async Task<ObjectRestoreRequestDto> Handle(RequestObjectRestoreCommand command, CancellationToken cancellationToken)
    {
        var requestedBy = currentUser.UserId ?? throw new UnauthorizedException();
        var request = ObjectRestoreRequest.Create(command.ObjectType, command.ObjectId, command.SnapshotDate, command.Justification, requestedBy);
        admin.AddObjectRestore(request);
        audit.Record(AuditEventTypes.ObjectRestoreRequested, AuditTargetTypes.ObjectRestore, request.Id,
            after: new { request.ObjectType, request.ObjectId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToDto();
    }
}

public sealed record DecideObjectRestoreCommand(Guid Id, bool Approve, string? Comment) : ICommand<ObjectRestoreRequestDto>;

public sealed class DecideObjectRestoreCommandHandler(
    IAdministrationRepository admin, ICurrentUser currentUser, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<DecideObjectRestoreCommand, ObjectRestoreRequestDto>
{
    public async Task<ObjectRestoreRequestDto> Handle(DecideObjectRestoreCommand command, CancellationToken cancellationToken)
    {
        var decidedBy = currentUser.UserId ?? throw new UnauthorizedException();
        var request = await admin.GetObjectRestoreAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Restore request", command.Id);

        if (command.Approve)
        {
            request.Approve(decidedBy);
        }
        else
        {
            request.Reject(decidedBy, command.Comment ?? "Rejected");
        }

        audit.Record(AuditEventTypes.ObjectRestoreDecided, AuditTargetTypes.ObjectRestore, request.Id,
            after: new { status = request.Status.ToString(), decidedBy });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToDto();
    }
}
