namespace AuditX.Application.Common.Messaging;

/// <summary>A command that mutates state and yields <typeparamref name="TResult"/>.</summary>
public interface ICommand<TResult>;

/// <summary>A read-only query yielding <typeparamref name="TResult"/>.</summary>
public interface IQuery<TResult>;

/// <summary>Handles a single command type.</summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handles a single query type.</summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// In-process mediator. Resolves the handler for a command/query, runs the validation pipeline, and
/// invokes it. Avoids a third-party mediator dependency (see ADR-0001).
/// </summary>
public interface IDispatcher
{
    Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    Task<TResult> Query<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
