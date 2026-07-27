using LibraryManagement.Shared.Domain.Results;
using Mediator;

namespace LibraryManagement.Shared.Application.CQS;

/// <summary>
/// Marker interface for commands, independent of the result they report.
/// </summary>
/// <remarks>
/// Lets a cross-cutting concern — validation, logging, auditing — take a command without naming the
/// result type it reports, which such a concern never needs.
/// </remarks>
public interface ICommandBase : IMessage
{
}

/// <summary>
/// Represents a command that returns a <see cref="Result"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result returned by the command.</typeparam>
/// <remarks>
/// There is no valueless form of this interface. A command returns no data, but it always reports
/// whether it did its work, and <see cref="Result"/> is exactly that report: success or errors, and
/// nothing else. <c>ICommand&lt;Result&gt;</c> is therefore already the command that returns no
/// value, and the constraint keeps <c>ICommand&lt;Result&lt;T&gt;&gt;</c> from compiling.
/// </remarks>
public interface ICommand<out TResult> : ICommandBase, IRequest<TResult>
    where TResult : Result
{
}

/// <summary>
/// Represents the handler for a command that returns a <see cref="Result"/>.
/// </summary>
/// <typeparam name="TCommand">The type of the command being handled.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the command.</typeparam>
public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : Result
{
}
