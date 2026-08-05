using LibraryManagement.Circulation.PublishedLanguage;

namespace LibraryManagement.Composition.Tests;

/// <summary>
/// The host's stand-in for the module that does not exist yet. Circulation declared
/// <see cref="IMemberBalance"/> for Charges to implement; until Charges does, the composition
/// answers, and this is its statement of what "no Charges module yet" means: nobody owes
/// anything.
/// </summary>
/// <remarks>
/// It lives in the composition and not in Circulation's registration on purpose — a module that
/// supplied its own answer to another module's question would never notice the real one arriving.
/// The day <c>AddChargesModule</c> exists, its registration replaces this line, and nothing else
/// moves.
/// </remarks>
internal sealed class NoChargesYet : IMemberBalance
{
    public ValueTask<decimal> OwedByAsync(Guid memberId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(0m);
}
