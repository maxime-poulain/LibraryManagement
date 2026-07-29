using System.Diagnostics;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using Mediator;
using Microsoft.Extensions.Logging;

namespace LibraryManagement.Shared.Infrastructure.Behaviors;

/// <summary>
/// Writes one line per message: what ran, how it ended, how long it took.
/// </summary>
/// <typeparam name="TMessage">Any command or query.</typeparam>
/// <typeparam name="TResponse">Its result, whichever of the two result types that is.</typeparam>
/// <param name="loggers">Creates the logger this behavior writes through.</param>
/// <remarks>
/// <para>
/// One behavior for every message is what makes the log exhaustive: no handler writes a line, so
/// no handler can forget to. The handlers stay silent by design — the <see cref="Result"/> already
/// carries the outcome up, and this behavior reports it exactly once, at the top. A handler
/// logging its own failure would say everything twice.
/// </para>
/// <para>
/// <strong>The line carries the message's name and its error codes, never its contents.</strong>
/// A command's fields are what an employee typed, and an error's <em>message</em> interpolates
/// them back — "'…' is already the authorized name" names a person. What a member borrows or asks
/// for is confidential by professional ethics before it is personal data by law, so the payload
/// and the error messages stay out of the log wholesale rather than field by field. The codes
/// alone answer the operational question — what failed, how often, why.
/// </para>
/// <para>
/// The category is the message's own type name rather than this behavior's, which is why the
/// factory is injected instead of an <c>ILogger&lt;T&gt;</c>: a host can then raise or silence one
/// command's lines in configuration, exactly as it does for any namespace.
/// </para>
/// <para>
/// A command that succeeds is a business fact and logs at Information; a query is traffic and
/// logs at Debug. A refusal logs at Warning whichever it was — one refusal is an employee being
/// told no, a spike of them is something worth an operator's glance, and the level is what makes
/// the spike visible. An exception logs at Error and is rethrown untouched: expected failures
/// travel as results, so whatever throws here is a defect, and this is the one place that still
/// knows which message it was. Cancellation is passed through silently — it is the caller's, not
/// news.
/// </para>
/// <para>
/// First in the pipeline the composition root declares — <c>options.PipelineBehaviors</c> on
/// <c>AddMediator</c>, where the whole pipeline is stated as one ordered array — so it is
/// outermost: what validation refuses is still a line, and the duration covers the whole
/// pipeline. A test in the composition project pins that order.
/// </para>
/// </remarks>
public sealed class LoggingBehavior<TMessage, TResponse>(ILoggerFactory loggers)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IFailable<TResponse>
{
    // Decided once per closed type: what a message is called and which kind it is never changes.
    private static readonly string MessageName = typeof(TMessage).Name;

    private static readonly bool IsCommand = typeof(ICommandBase).IsAssignableFrom(typeof(TMessage));

    private readonly ILogger _logger = loggers.CreateLogger(typeof(TMessage).FullName!);

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();

        TResponse response;

        try
        {
            response = await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var elapsedToCrash = Elapsed(start);
            PipelineLog.Crashed(_logger, MessageName, elapsedToCrash, exception);
            throw;
        }

        var elapsed = Elapsed(start);

        if (response.HasErrors())
        {
            // Joined only when someone listens: the codes are the one argument here that costs.
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                PipelineLog.Refused(_logger, MessageName, CodesOf(response), elapsed);
            }
        }
        else if (IsCommand)
        {
            PipelineLog.CommandHandled(_logger, MessageName, elapsed);
        }
        else
        {
            PipelineLog.QueryAnswered(_logger, MessageName, elapsed);
        }

        return response;
    }

    private static double Elapsed(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    // The codes and never the error messages — the messages interpolate what was typed.
    private static string CodesOf(TResponse response)
    {
        var codes = string.Empty;
        response.TapError(errors => codes = string.Join(", ", errors.Select(error => error.ErrorCode)));

        return codes;
    }
}

/// <summary>
/// The pipeline's log lines, source-generated. A companion type because the behavior is generic
/// and the <see cref="LoggerMessageAttribute"/> generator does not reach into generic types.
/// </summary>
internal static partial class PipelineLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "{MessageName} handled in {ElapsedMilliseconds} ms.")]
    public static partial void CommandHandled(ILogger logger, string messageName, double elapsedMilliseconds);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "{MessageName} answered in {ElapsedMilliseconds} ms.")]
    public static partial void QueryAnswered(ILogger logger, string messageName, double elapsedMilliseconds);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "{MessageName} refused with [{ErrorCodes}] in {ElapsedMilliseconds} ms.")]
    public static partial void Refused(ILogger logger, string messageName, string errorCodes, double elapsedMilliseconds);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error,
        Message = "{MessageName} crashed after {ElapsedMilliseconds} ms.")]
    public static partial void Crashed(ILogger logger, string messageName, double elapsedMilliseconds, Exception exception);
}
