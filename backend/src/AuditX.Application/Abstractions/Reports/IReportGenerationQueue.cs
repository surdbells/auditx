namespace AuditX.Application.Abstractions.Reports;

/// <summary>
/// Enqueues a report's generation to run asynchronously off the request thread (M8). The implementation is a
/// fire-and-forget Hangfire job (the report id IS the job handle — D1). Abstracted so the application command
/// handler stays free of the background-job framework.
/// </summary>
public interface IReportGenerationQueue
{
    void Enqueue(Guid reportId);
}
