using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Application.Common.Messaging;

/// <summary>
/// Reflection-based dispatcher with a FluentValidation pre-handler pipeline. Handler/validator types
/// are resolved per request from the DI container; closed generic method handles are cached.
/// </summary>
public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, Type> CommandHandlerTypes = new();
    private static readonly ConcurrentDictionary<Type, Type> QueryHandlerTypes = new();

    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        => InvokeAsync<TResult>(command, isCommand: true, cancellationToken);

    public Task<TResult> Query<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        => InvokeAsync<TResult>(query, isCommand: false, cancellationToken);

    private async Task<TResult> InvokeAsync<TResult>(object message, bool isCommand, CancellationToken cancellationToken)
    {
        var messageType = message.GetType();

        await ValidateAsync(message, messageType, cancellationToken);

        var handlerType = isCommand
            ? CommandHandlerTypes.GetOrAdd(messageType, mt => typeof(ICommandHandler<,>).MakeGenericType(mt, typeof(TResult)))
            : QueryHandlerTypes.GetOrAdd(messageType, mt => typeof(IQueryHandler<,>).MakeGenericType(mt, typeof(TResult)));

        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod("Handle")!;

        try
        {
            return await (Task<TResult>)method.Invoke(handler, [message, cancellationToken])!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Surface the real exception (DomainException, NotFoundException, ...) rather than the wrapper.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }
    }

    private async Task ValidateAsync(object message, Type messageType, CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(messageType);
        var validators = serviceProvider.GetServices(validatorType).Cast<IValidator>().ToArray();
        if (validators.Length == 0)
        {
            return;
        }

        var context = new ValidationContext<object>(message);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
