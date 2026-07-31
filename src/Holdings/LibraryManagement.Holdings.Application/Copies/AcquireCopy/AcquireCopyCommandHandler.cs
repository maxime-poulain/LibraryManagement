using LibraryManagement.Catalog.PublishedLanguage;
using LibraryManagement.Holdings.Domain;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.AcquireCopy;

/// <summary>
/// Handles <see cref="AcquireCopyCommand"/>.
/// </summary>
/// <param name="copies">The store to add the new copy to, and the only thing that can see the other labels.</param>
/// <param name="editions">Catalog's published language, consulted to confirm the edition exists.</param>
/// <remarks>
/// <para>
/// The one moment in this context with a precondition that leaves it. Holdings holds an
/// <c>EditionId</c> and nothing else of the bibliographic record, and no foreign key backs that
/// reference — an integrity constraint across schemas is a coupling the compiler cannot see. So the
/// question is asked here, of the module that owns the answer, through the surface that module
/// publishes.
/// </para>
/// <para>
/// <see cref="IEditionCatalog"/> is not an anticorruption layer, and it is worth being clear why one
/// is not needed: an identifier goes out and a yes or no comes back, so no concept of Catalog's ever
/// reaches a decision here. The day the bibliographic summary crosses — to render a line, to compose
/// something — that changes, and a translation on this side becomes right.
/// </para>
/// <para>
/// Both refusals are collected before either is reported, the way <c>RegisterWorkCommandHandler</c>
/// collects a work's missing authors. A librarian holding a trolley would rather be told about the
/// barcode and the edition at once than discover the second after fixing the first.
/// </para>
/// </remarks>
public sealed class AcquireCopyCommandHandler(ICopyRepository copies, IEditionCatalog editions)
    : ICommandHandler<AcquireCopyCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        AcquireCopyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new ErrorCollection();

        var barcode = Barcode.Create(command.Barcode);
        barcode.TapError(errors.AddErrors);

        var shelfmark = Shelfmark.Create(command.Shelfmark);
        shelfmark.TapError(errors.AddErrors);

        // Only worth asking once the label is one: a malformed barcode cannot collide with anything.
        await barcode.MatchAsync(
            async label =>
            {
                var taken = await copies
                    .BarcodeIsTakenAsync(label, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (taken)
                {
                    errors.Add(
                        HoldingsErrorCodes.BarcodeAlreadyInUse,
                        $"Barcode '{label}' is already on another copy.");
                }

                return Result.Success();
            },
            _ => ValueTask.FromResult(Result.Success())).ConfigureAwait(false);

        var editionExists = await editions
            .ExistsAsync(command.EditionId, cancellationToken)
            .ConfigureAwait(false);

        if (!editionExists)
        {
            errors.Add(
                HoldingsErrorCodes.EditionNotFound,
                $"No edition is cataloged under '{command.EditionId}', so nothing can be a copy of it.");
        }

        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        return barcode.Bind(label => shelfmark.Bind(mark =>
        {
            var copy = Copy.Acquire(
                CopyId.Create(command.CopyId),
                EditionId.Create(command.EditionId),
                label,
                mark,
                command.Condition,
                command.AcquiredOn,
                command.ReferenceOnly);

            copies.Add(copy);

            return Result.Success();
        }));
    }
}
