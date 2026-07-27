using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Application.Errors;

/// <summary>
/// The few error codes owned by the shared kernel itself. Everything else belongs to the bounded
/// context that raises it.
/// </summary>
/// <remarks>
/// The rule elsewhere is that a module declares its own codes, prefixed with its own name. These
/// are the exception because they describe outcomes of the dispatch pipeline rather than of any
/// one module's rules: every module can produce them, and none of them owns the concept.
/// </remarks>
public static class SharedErrorCodes
{
    /// <summary>
    /// Another transaction changed the same aggregate first, and this one was rejected.
    /// </summary>
    /// <remarks>
    /// This is an expected outcome, not a bug: two employees acting on the same loan or the same
    /// copy at the same moment. It reaches the caller as a failed result rather than as an
    /// exception, so the caller can decide whether to reload and retry.
    /// </remarks>
    public static readonly ErrorCode ConcurrencyConflict = new("Shared.ConcurrencyConflict");

    /// <summary>
    /// A command was rejected before it ran because its input was not well formed.
    /// </summary>
    /// <remarks>
    /// One code for every field problem, on purpose: which field is at fault is carried by
    /// <see cref="Error.Target"/> and what is wrong by the message. Minting a code per field would
    /// leave the set open-ended and mix two questions into one string.
    /// </remarks>
    public static readonly ErrorCode ValidationFailed = new("Shared.ValidationFailed");
}
