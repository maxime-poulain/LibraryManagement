using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.ReleaseTrappedCopy;

/// <summary>
/// Takes back the promise a copy carried, because the copy left service before the borrower came
/// for it.
/// </summary>
/// <param name="CopyId">The copy that is no longer there to be collected.</param>
/// <remarks>
/// The reaction to Holdings announcing a departure from service. Without it, a copy weeded or
/// water-damaged off the hold shelf left its claim standing: the borrower travelled for a copy
/// that was not there, the claim expired at its deadline, and the member was counted a no-show
/// for the library's own doing. The claim goes back to the queue at the place its age gives it —
/// the borrower did nothing and loses nothing but the trip they are now spared.
/// </remarks>
public sealed record ReleaseTrappedCopyCommand(Guid CopyId) : ICommand<Result>;
