using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Host.Http;

/// <summary>
/// What every endpoint in this host is: a command bound from the body, dispatched, and mapped.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The command record is the request body.</strong> No request type stands between them,
/// because there would be nothing for it to do: a command is already a flat record of primitives
/// that a validator refuses when it is wrong, and a parallel type would be a second place to add
/// a field and a first place to forget one.
/// </para>
/// <para>
/// <strong>Identifiers are the caller's.</strong> Every creating command takes the new identifier
/// first, which is what makes a repeated POST harmless: the second one collides on the key rather
/// than minting a second aggregate. A host that generated them would turn a retry after a timeout
/// into a duplicate member.
/// </para>
/// <para>
/// <strong>Only what a librarian does is routed.</strong> Seven commands belong to the daily
/// process, and several more exist because one module reacts to another — a replacement charge
/// raised by a write-off, a hold released because the copy left service. Those have no desk
/// audience, and a route for them would let a request forge a fact only the announcing module is
/// entitled to state.
/// </para>
/// </remarks>
internal static class DeskEndpoints
{
    /// <summary>
    /// Maps a command onto the group: bound from the body, dispatched, mapped to a response.
    /// </summary>
    /// <typeparam name="TCommand">The command this route carries.</typeparam>
    /// <param name="group">The module's route group.</param>
    /// <param name="route">The route, relative to the group.</param>
    /// <returns>The same group, so calls can be chained.</returns>
    public static RouteGroupBuilder Command<TCommand>(this RouteGroupBuilder group, string route)
        where TCommand : ICommand<Result>
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost(
            route,
            async (TCommand command, ICommandDispatcher commands, CancellationToken cancellationToken)
                => (await commands.DispatchAsync(command, cancellationToken).ConfigureAwait(false))
                    .ToHttpResult());

        return group;
    }
}
