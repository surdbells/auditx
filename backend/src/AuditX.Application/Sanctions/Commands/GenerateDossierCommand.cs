using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Abstractions.Sanctions;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;

namespace AuditX.Application.Sanctions.Commands;

/// <summary>The generated dossier returned to the controller for download (and persisted under sanctions_dossiers/).</summary>
public sealed record DossierResult(byte[] Content, string ContentType, string Filename, string Sha256Hash);

public sealed record GenerateDossierCommand(Guid CaseId) : ICommand<DossierResult>;

public sealed class GenerateDossierCommandHandler(
    ISanctionsCaseRepository cases, IExceptionRepository exceptions, IEvidenceRepository evidence,
    IDossierGenerator generator, IFileStorage storage, IPermissionResolver permissions, ICurrentUser currentUser,
    IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<GenerateDossierCommand, DossierResult>
{
    public async Task<DossierResult> Handle(GenerateDossierCommand command, CancellationToken cancellationToken)
    {
        var sanctionsCase = await cases.GetByIdAsync(command.CaseId, cancellationToken) ?? throw new NotFoundException("Sanctions case", command.CaseId);

        // Team check: only a case-team member may pull the dossier (it carries the subject context).
        if (!await SanctionsAccess.IsCaseTeamMemberAsync(sanctionsCase, currentUser.UserId, permissions, cancellationToken))
        {
            throw new ForbiddenAccessException("Only a case-team member may generate the dossier.");
        }

        var exception = await exceptions.GetByIdAsync(sanctionsCase.ExceptionId, cancellationToken);

        // Evidence references: hashes only (US-M7-007), gathered from the exception's MAP-action evidence.
        var references = new List<DossierEvidenceReference>();
        if (exception is not null)
        {
            foreach (var action in exception.MapActions)
            {
                var files = await evidence.ListForContextAsync(exception.AuditId, EvidenceContextType.MapAction, action.Id, cancellationToken);
                references.AddRange(files.Select(f => new DossierEvidenceReference(f.OriginalFilename, f.Sha256Hash)));
            }
        }

        // The dossier renders the grid version/range PINNED on the case at recommendation time (carried on the
        // case itself), not the live active grid which may have changed since.
        var context = new DossierContext(
            sanctionsCase, exception?.Title, exception?.Severity.ToString(), exception?.Category, references);
        var artefact = generator.Generate(context);

        var key = $"sanctions_dossiers/{sanctionsCase.Id}/{Guid.NewGuid():N}.html";
        await storage.SaveAsync(key, artefact.Content, cancellationToken);

        // The dossier exposes the subject's context to the viewer — record the deliberate exposure (US-M7-016).
        audit.Record(AuditEventTypes.SubjectIdentityExposed, AuditTargetTypes.SanctionsCase, sanctionsCase.Id,
            payload: new { viewer = currentUser.UserId, via = "dossier" });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DossierResult(artefact.Content, artefact.ContentType, artefact.Filename, artefact.Sha256Hash);
    }
}
