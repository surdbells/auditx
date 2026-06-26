using AuditX.Domain.Identity;

namespace AuditX.Application.Abstractions.MakerChecker;

/// <summary>Persistence + lookup for maker-checker gate configuration.</summary>
public interface IMakerCheckerGateRepository
{
    Task<MakerCheckerGate?> GetByActionTypeAsync(string actionType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MakerCheckerGate>> GetAllAsync(CancellationToken cancellationToken = default);

    void Add(MakerCheckerGate gate);
}

/// <summary>
/// Replays an approved pending action. One implementation is registered per gateable action type; the
/// approval handler resolves the matching executor by <see cref="ActionType"/> and runs it inside the
/// approval transaction so approval and execution are atomic (US-M1-022).
/// </summary>
public interface IPendingActionExecutor
{
    string ActionType { get; }

    /// <summary>Execute the captured action, attributing it to the original maker.</summary>
    Task ExecuteAsync(string pendingPayloadJson, Guid makerUserId, CancellationToken cancellationToken = default);
}
