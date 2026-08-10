using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Charges.Application.Accounts.GetMemberBalance;

/// <summary>
/// Reads what one member currently owes.
/// </summary>
/// <param name="MemberId">The member whose account to read.</param>
/// <remarks>
/// <para>
/// The desk-facing twin of the port Circulation asks through. They answer the same figure and are
/// deliberately not the same thing: <c>IMemberBalance</c> is a contract this module owes another
/// module, declared by that module in its own published language, and it exists so a checkout can
/// be refused. This is a query in this module's own application layer, and it exists so a screen
/// can show a number. Collapsing the two would put a page's needs inside a contract Circulation
/// wrote, and the next field a screen wants would arrive as a change to somebody else's port.
/// </para>
/// <para>
/// A member with no account owes nothing, and that is an answer rather than a failure: an account
/// comes into being when the first charge is raised, so its absence is the ordinary case for most
/// of the membership.
/// </para>
/// </remarks>
public sealed record GetMemberBalanceQuery(Guid MemberId) : IQuery<MemberBalanceDto>;
