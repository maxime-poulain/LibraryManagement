namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// Finds and tracks accounts.
/// </summary>
/// <remarks>
/// Declared here and implemented in infrastructure, the dependency inversion every module in this
/// solution follows. It never saves: <c>UnitOfWorkBehavior</c> writes once, on success, through the
/// module's unit of work.
/// </remarks>
public interface IMemberAccountRepository
{
    /// <summary>
    /// Finds a member's account, with the charges it holds.
    /// </summary>
    /// <param name="memberId">The member.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The account, or <see langword="null"/> when the member has never been charged.</returns>
    Task<MemberAccount?> GetByMemberAsync(MemberId memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the account holding an outstanding replacement charge for a copy.
    /// </summary>
    /// <param name="copyId">The copy that has turned up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The account, or <see langword="null"/> when nothing is outstanding for that copy.</returns>
    /// <remarks>
    /// The reversal arrives from Holdings naming a copy and nothing else — no member, no loan — so
    /// this is the one lookup that reaches an account by something other than its own identity.
    /// </remarks>
    Task<MemberAccount?> GetByOutstandingReplacementForAsync(
        CopyId copyId,
        CancellationToken cancellationToken = default);

    /// <summary>Tracks a new account.</summary>
    /// <param name="account">The account to add.</param>
    void Add(MemberAccount account);
}
