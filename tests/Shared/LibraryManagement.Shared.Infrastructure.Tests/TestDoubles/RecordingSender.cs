using Mediator;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

// A stand-in for Mediator's ISender. Only the IRequest<T> overload is reachable from the
// dispatchers, because the kernel's ICommand<T> and IQuery<T> derive from IRequest<T> and not from
// Mediator's own ICommand<T> / IQuery<T>. The rest throws, so an unexpected route is loud.
public sealed class RecordingSender(Func<object, object> respond) : ISender
{
    public object? LastMessage { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public int SendCount { get; private set; }

    public static RecordingSender Returning(object response) => new(_ => response);

    public static RecordingSender Throwing(Exception exception) => new(_ => throw exception);

    public ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        LastMessage = request;
        LastCancellationToken = cancellationToken;
        SendCount++;

        return ValueTask.FromResult((TResponse)respond(request));
    }

    public ValueTask<TResponse> Send<TResponse>(
        Mediator.ICommand<TResponse> command,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The kernel's commands travel through the IRequest overload.");

    public ValueTask<TResponse> Send<TResponse>(
        Mediator.IQuery<TResponse> query,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The kernel's queries travel through the IRequest overload.");

    public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The kernel's messages travel through the typed overload.");

    public IAsyncEnumerable<object?> CreateStream(object message, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamCommand<TResponse> command,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamQuery<TResponse> query,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
