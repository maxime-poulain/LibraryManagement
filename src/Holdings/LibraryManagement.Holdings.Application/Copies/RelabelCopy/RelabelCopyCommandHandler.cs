using LibraryManagement.Holdings.Domain;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.RelabelCopy;

/// <summary>
/// Handles <see cref="RelabelCopyCommand"/>.
/// </summary>
/// <param name="copies">The store holding the copy, and the only thing that can see the other labels.</param>
/// <remarks>
/// A barcode identifies one copy in the library, and a copy cannot see the other copies — so the
/// rule spans the whole set and the handler asks the question, exactly as crediting an author does
/// in Catalog. It stays a business rule and not a bare constraint: the refusal names the label,
/// which a unique-index violation cannot do.
/// </remarks>
public sealed class RelabelCopyCommandHandler(ICopyRepository copies)
    : ICommandHandler<RelabelCopyCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RelabelCopyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var copyId = CopyId.Create(command.CopyId);
        var copy = await copies.GetByIdAsync(copyId, cancellationToken).ConfigureAwait(false);

        if (copy is null)
        {
            return Result.Failure(
                HoldingsErrorCodes.CopyNotFound,
                $"No copy is held under '{copyId}'.");
        }

        var barcode = Barcode.Create(command.Barcode);

        return await barcode.MatchAsync(
            async label =>
            {
                // Excluding this copy, so relabelling one to the label it already carries is not
                // reported as a collision with itself. The aggregate treats that as a no-op; being
                // told it collides with itself would be a lie on the way there.
                var taken = await copies
                    .BarcodeIsTakenAsync(label, copyId, cancellationToken)
                    .ConfigureAwait(false);

                return taken
                    ? Result.Failure(
                        HoldingsErrorCodes.BarcodeAlreadyInUse,
                        $"Barcode '{label}' is already on another copy.")
                    : copy.Relabel(label);
            },
            errors => ValueTask.FromResult(Result.Failure(errors))).ConfigureAwait(false);
    }
}
