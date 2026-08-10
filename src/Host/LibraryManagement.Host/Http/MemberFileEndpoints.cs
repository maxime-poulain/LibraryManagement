using LibraryManagement.Charges.Application.Accounts.GetMemberBalance;
using LibraryManagement.Circulation.Application.Borrowers.GetBorrowerFile;
using LibraryManagement.Members.Application.Members.GetMemberById;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Host.Http;

/// <summary>
/// The one page in this host that is composed rather than routed: a member's file, assembled at
/// the edge from three modules' published queries.
/// </summary>
/// <remarks>
/// <para>
/// <strong>It lives here and not in <c>MembersEndpoints</c> because it belongs to no module.</strong>
/// That is the whole point of the arrangement <c>strategic-design.md</c> §10 decides: a read that
/// starts from an identifier already in hand needs no cross-module SQL and no projection — the
/// host dispatches each module's own query and puts the answers side by side. Filing it under
/// Members would suggest Members owns it, and the next reader would look for the loans there.
/// </para>
/// <para>
/// <strong>The composer assembles and decides nothing.</strong> Whether this borrower may still
/// borrow is <c>Standing</c>, judged by Circulation against its own policy and displayed here
/// exactly as given. The temptation is real and cheap to give in to — the balance is right there in
/// the same response, and comparing it to a threshold is one line — and it is the one thing this
/// file must not do: a rule that slips into a composer is a rule no module's invariants cover, and
/// no test of any module would ever catch it drifting.
/// </para>
/// <para>
/// <strong>The queries run one after another.</strong> Not for want of ambition: the contexts
/// behind them are scoped and not thread-safe, so a page that fanned out would be sharing a
/// <c>DbContext</c> across threads. Three sequential reads of a handful of rows is what a desk
/// screen costs, and the day that is measurably too slow the answer is a read model, which §7
/// already names.
/// </para>
/// </remarks>
internal static class MemberFileEndpoints
{
    /// <summary>
    /// Maps the composed member file.
    /// </summary>
    /// <param name="routes">The host's route builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapMemberFile(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/members/{memberId:guid}/file", ComposeAsync).WithTags("Members");

        return routes;
    }

    /// <summary>
    /// Asks the three modules and puts their answers side by side.
    /// </summary>
    /// <param name="memberId">The member whose file to build.</param>
    /// <param name="queries">The dispatcher each module's query goes through.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The file, or the problem document Members answered with.</returns>
    /// <remarks>
    /// A named method rather than a lambda in the route, so the degradation below can be tested
    /// without a database: what happens when a module cannot answer is a decision
    /// (<c>docs/adr/0016-a-composed-page-degrades-in-parts.md</c>), and a decision that only an
    /// integration test can reach is one that goes unchecked in the gate that actually blocks a
    /// merge.
    /// </remarks>
    internal static async Task<IResult> ComposeAsync(
        Guid memberId,
        IQueryDispatcher queries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queries);

        var member = await queries
            .DispatchAsync(new GetMemberByIdQuery(memberId), cancellationToken)
            .ConfigureAwait(false);

        // The one answer whose absence empties the page: a file is a file *of* somebody, and there
        // is nothing to show around a person the library never enrolled. So this failure is the
        // caller's, mapped as any query's is — 404 when nobody is enrolled under that identifier.
        // The other two are different in kind, below.
        var details = member.Match<MemberDetailsDto?>(value => value, _ => null);

        if (details is null)
        {
            return member.ToHttpResult();
        }

        var unavailable = new List<UnavailablePartResponse>();

        var circulation = (await queries
                .DispatchAsync(new GetBorrowerFileQuery(memberId), cancellationToken)
                .ConfigureAwait(false))
            .Match<BorrowerFileDto?>(
                value => value,
                errors =>
                {
                    unavailable.Add(UnavailablePartResponse.From("circulation", errors));
                    return null;
                });

        var charges = (await queries
                .DispatchAsync(new GetMemberBalanceQuery(memberId), cancellationToken)
                .ConfigureAwait(false))
            .Match<MemberBalanceDto?>(
                value => value,
                errors =>
                {
                    unavailable.Add(UnavailablePartResponse.From("charges", errors));
                    return null;
                });

        return Results.Ok(new MemberFileResponse(details, circulation, charges, unavailable));
    }
}

/// <summary>
/// A member's file as the desk reads it: the person, what they have out, and what they owe.
/// </summary>
/// <param name="Member">Who the person is, from Members.</param>
/// <param name="Circulation">What they have out and are waiting for, from Circulation, or
/// <see langword="null"/> when that module could not answer.</param>
/// <param name="Charges">What they owe, from Charges, or <see langword="null"/> when that module
/// could not answer.</param>
/// <param name="Unavailable">The parts that are missing and why. Empty on a whole file.</param>
/// <remarks>
/// <para>
/// The page's shape, and therefore the host's type. No module carries a DTO shaped like somebody's
/// screen: this one nests three modules' answers unchanged, so each context keeps saying exactly
/// what it says and the arrangement of them is presentation.
/// </para>
/// <para>
/// Named <c>Response</c> rather than <c>Dto</c> on purpose. The suffix marks a query's answer, and
/// an architecture rule enforces it there; this is not one — it is what an endpoint returns, and
/// giving it the same suffix would file the page's shape alongside the modules' contracts.
/// </para>
/// </remarks>
public sealed record MemberFileResponse(
    MemberDetailsDto Member,
    BorrowerFileDto? Circulation,
    MemberBalanceDto? Charges,
    IReadOnlyList<UnavailablePartResponse> Unavailable);

/// <summary>
/// One part of the file that could not be read, named so the desk knows what it is not seeing.
/// </summary>
/// <param name="Part">Which part is missing — <c>circulation</c> or <c>charges</c>.</param>
/// <param name="Code">The error code the module answered with.</param>
/// <param name="Detail">What that module said, for whoever has to fix it.</param>
public sealed record UnavailablePartResponse(string Part, string Code, string Detail)
{
    /// <summary>
    /// Names a missing part from the errors its module answered with.
    /// </summary>
    /// <param name="part">Which part is missing.</param>
    /// <param name="errors">What the module answered.</param>
    /// <returns>The part, named.</returns>
    /// <remarks>
    /// The first error only, where the problem document for an outright failure carries every one.
    /// The difference is who is reading: a problem document is handed to a caller who must correct
    /// something, and hiding faults from them makes them iterate; this line is a note on a screen
    /// saying which panel is blank, and a librarian who cannot act on one reason cannot act on four.
    /// </remarks>
    internal static UnavailablePartResponse From(string part, IReadOnlyErrorCollection errors)
        => new(part, errors[0].ErrorCode.Value, errors[0].ErrorMessage);
}
