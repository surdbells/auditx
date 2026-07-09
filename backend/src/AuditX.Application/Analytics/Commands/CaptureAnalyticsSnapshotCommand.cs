using AuditX.Application.Analytics.Services;
using AuditX.Application.Common.Messaging;

namespace AuditX.Application.Analytics.Commands;

/// <summary>Forces an immediate KPI snapshot capture for today (also driven daily by the background job). Returns the row count.</summary>
public sealed record CaptureAnalyticsSnapshotCommand : ICommand<int>;

public sealed class CaptureAnalyticsSnapshotCommandHandler(AnalyticsSnapshotCaptureService capture)
    : ICommandHandler<CaptureAnalyticsSnapshotCommand, int>
{
    public Task<int> Handle(CaptureAnalyticsSnapshotCommand command, CancellationToken cancellationToken)
        => capture.CaptureAsync(cancellationToken);
}
