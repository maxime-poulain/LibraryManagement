namespace LibraryManagement.Charges.Application.Accounts.GetMemberBalance;

/// <summary>
/// What a member currently owes: every charge and payment netted.
/// </summary>
/// <param name="MemberId">The member whose account this is.</param>
/// <param name="Balance">The amount outstanding. Zero when nothing is owed, and zero when no
/// account was ever opened — the two are the same fact to everyone who asks.</param>
/// <remarks>
/// <para>
/// <strong>A statement of money, never of rights.</strong> This context's whole vocabulary is the
/// amount: what being owed forbids is Circulation's judgement, made against a threshold this
/// module has no opinion about. A boolean here would move that rule into the wrong context and
/// the words would follow it — which is precisely why the glossary gives one figure two names,
/// <c>Balance</c> here and <c>Debt</c> there.
/// </para>
/// <para>
/// <strong>The charges themselves are deliberately not itemized.</strong> A member's file asks
/// what they owe, and the answer to that is one number. A librarian taking a payment needs the
/// breakdown, but that is a different screen with a different question — which charge is being
/// settled — and a query shaped for it would be shaped by the payment moment rather than by the
/// file. Adding it later costs nothing; carrying it now would make every reader of a file
/// materialize an account's whole history to display a total.
/// </para>
/// </remarks>
public sealed record MemberBalanceDto(Guid MemberId, decimal Balance);
