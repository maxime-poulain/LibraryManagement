namespace LibraryManagement.Circulation.PublishedLanguage;

/// <summary>
/// The one question Circulation asks of Charges: how much does this member owe?
/// </summary>
/// <remarks>
/// <para>
/// <strong>The dependency-inverted edge of the context map, made concrete.</strong> Circulation is
/// downstream of Charges for this question, and the anticorruption layer belongs to the
/// downstream — so Circulation declares the port, for its own single use, and Charges implements
/// it, in that module's own infrastructure and against its own store. The inversion is what keeps
/// the cycle the context map draws out of the assembly graph: this project references nothing, and
/// Charges references it rather than the other way round. Not an Open Host Service, though it looks
/// like one: an OHS is a protocol published <em>by the upstream</em> for an open set of consumers,
/// and here the consumer wrote the contract.
/// </para>
/// <para>
/// <strong>It answers with an amount and never with a verdict.</strong> Charges states what is
/// owed; what being owed forbids — the borrower's standing — is judged on the asking side, by the
/// circulation policy. If this interface exposed <c>IsBlocked</c>, the rule would have moved into
/// the wrong context. The vocabulary splits the same way: the amount is a <em>balance</em> where
/// Charges stands and a <em>debt</em> where Circulation reads it, and this port speaks neither —
/// it hands over a number.
/// </para>
/// <para>
/// Asked synchronously, at the counter, with the member standing there having possibly just paid:
/// a projection lag would be visible, which is why this is a port and not an event. It sits on the
/// most frequent write path of the system, and it is an in-process call only because this is a
/// monolith — a documented reason the deployment stays one.
/// </para>
/// </remarks>
public interface IMemberBalance
{
    /// <summary>
    /// Answers what a member currently owes, every charge and payment netted.
    /// </summary>
    /// <param name="memberId">The member at the desk. The same value Circulation holds as its
    /// <c>BorrowerId</c> — the boundary translates the model, never the identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The amount owed. Zero when nothing is.</returns>
    ValueTask<decimal> OwedByAsync(Guid memberId, CancellationToken cancellationToken = default);
}
