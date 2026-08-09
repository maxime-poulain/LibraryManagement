using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.NoteCopyAccountedFor;

/// <summary>
/// Handles <see cref="NoteCopyAccountedForCommand"/>.
/// </summary>
/// <param name="copies">The store holding the copy, or not — both are answers here.</param>
/// <remarks>
/// Success whatever it finds, deliberately, because this arrives from another module's drain and
/// every quiet outcome is legitimate: a copy in good standing needs nothing; a copy this module
/// never heard of — or withdrew after the loan started — is not this fact's business; and only a
/// copy held as lost is actually found. <c>Find</c> is called through the aggregate so the
/// recovery announces itself exactly as a desk-side find would, memory and all.
/// </remarks>
public sealed class NoteCopyAccountedForCommandHandler(ICopyRepository copies)
    : ICommandHandler<NoteCopyAccountedForCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        NoteCopyAccountedForCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var copyId = CopyId.Create(command.CopyId);
        var copy = await copies.GetByIdAsync(copyId, cancellationToken).ConfigureAwait(false);

        if (copy is null || copy.Status != CopyStatus.Lost)
        {
            return Result.Success();
        }

        return copy.Find();
    }
}
