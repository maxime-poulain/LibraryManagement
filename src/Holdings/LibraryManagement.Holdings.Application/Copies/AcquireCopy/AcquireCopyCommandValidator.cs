using FluentValidation;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Application.Copies.AcquireCopy;

/// <summary>
/// Checks the shape of an <see cref="AcquireCopyCommand"/>.
/// </summary>
public sealed class AcquireCopyCommandValidator : AbstractValidator<AcquireCopyCommand>
{
    /// <summary>
    /// The earliest acquisition date a library will accept.
    /// </summary>
    /// <remarks>
    /// A fixed floor rather than a clock, for the reason <c>LifeYears</c> gives: the domain has no
    /// clock, and an implausible date is a question about the field, which a validator answers. No
    /// library in this system acquired anything before printing did.
    /// </remarks>
    public static readonly DateOnly EarliestAcquisition = new(1450, 1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="AcquireCopyCommandValidator"/> class.
    /// </summary>
    public AcquireCopyCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");

        RuleFor(command => command.EditionId)
            .NotEmpty()
            .WithMessage("An edition identifier is required.");

        RuleFor(command => command.Barcode)
            .NotEmpty()
            .WithMessage("A barcode is required.")
            .MinimumLength(Barcode.MinLength)
            .WithMessage($"A barcode must be at least {Barcode.MinLength} characters.")
            .MaximumLength(Barcode.MaxLength)
            .WithMessage($"A barcode may not exceed {Barcode.MaxLength} characters.");

        RuleFor(command => command.Shelfmark)
            .NotEmpty()
            .WithMessage("A shelfmark is required.")
            .MaximumLength(Shelfmark.MaxLength)
            .WithMessage($"A shelfmark may not exceed {Shelfmark.MaxLength} characters.");

        RuleFor(command => command.AcquiredOn)
            .GreaterThanOrEqualTo(EarliestAcquisition)
            .WithMessage($"An acquisition date may not precede {EarliestAcquisition:yyyy-MM-dd}.");

        RuleFor(command => command.Condition)
            .IsInEnum()
            .WithMessage("A condition must be one of good, worn or damaged.");
    }
}
