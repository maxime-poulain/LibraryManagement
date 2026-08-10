using FluentValidation.Results;
using LibraryManagement.Circulation.Application.Holds.CancelHold;
using LibraryManagement.Circulation.Application.Holds.PlaceHold;
using LibraryManagement.Circulation.Application.Loans.CheckOutCopy;
using LibraryManagement.Circulation.Application.Loans.RenewLoan;
using LibraryManagement.Circulation.Application.Loans.ReturnCopy;

namespace LibraryManagement.Circulation.Application.Tests;

/// <summary>
/// Five commands whose whole shape is identifiers, tested together rather than in five files
/// saying the same three things — the arrangement the Holdings identifier-only commands settled.
/// Everything else a desk moment refuses is state, not shape, and the handler tests own it.
///
/// Cancelling a hold names three of them since a merged queue may hold two claims of one borrower:
/// the pair of edition and borrower stopped identifying a claim the day two queues could become
/// one.
/// </summary>
public sealed class CommandValidatorTests
{
    private static readonly Guid Id = Guid.CreateVersion7();

    public static TheoryData<string> Commands => new(
        nameof(CheckOutCopyCommand),
        nameof(ReturnCopyCommand),
        nameof(RenewLoanCommand),
        nameof(PlaceHoldCommand),
        nameof(CancelHoldCommand));

    private static ValidationResult ValidateWellFormed(string command) => command switch
    {
        nameof(CheckOutCopyCommand)
            => new CheckOutCopyCommandValidator().Validate(new CheckOutCopyCommand(Id, Id, Id)),
        nameof(ReturnCopyCommand)
            => new ReturnCopyCommandValidator().Validate(new ReturnCopyCommand(Id)),
        nameof(RenewLoanCommand)
            => new RenewLoanCommandValidator().Validate(new RenewLoanCommand(Id)),
        nameof(PlaceHoldCommand)
            => new PlaceHoldCommandValidator().Validate(new PlaceHoldCommand(Id, Id, Id)),
        nameof(CancelHoldCommand)
            => new CancelHoldCommandValidator().Validate(new CancelHoldCommand(Id, Id, Id)),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };

    private static ValidationResult ValidateAllEmpty(string command) => command switch
    {
        nameof(CheckOutCopyCommand)
            => new CheckOutCopyCommandValidator()
                .Validate(new CheckOutCopyCommand(Guid.Empty, Guid.Empty, Guid.Empty)),
        nameof(ReturnCopyCommand)
            => new ReturnCopyCommandValidator().Validate(new ReturnCopyCommand(Guid.Empty)),
        nameof(RenewLoanCommand)
            => new RenewLoanCommandValidator().Validate(new RenewLoanCommand(Guid.Empty)),
        nameof(PlaceHoldCommand)
            => new PlaceHoldCommandValidator()
                .Validate(new PlaceHoldCommand(Guid.Empty, Guid.Empty, Guid.Empty)),
        nameof(CancelHoldCommand)
            => new CancelHoldCommandValidator()
                .Validate(new CancelHoldCommand(Guid.Empty, Guid.Empty, Guid.Empty)),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };

    private static int IdentifiersOf(string command) => command switch
    {
        nameof(CheckOutCopyCommand) or nameof(PlaceHoldCommand) or nameof(CancelHoldCommand) => 3,
        _ => 1,
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void AWellFormedCommand_IsAccepted(string command)
    {
        ValidateWellFormed(command).IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void EveryEmptyIdentifier_IsRejected_AndAllAtOnce(string command)
    {
        var outcome = ValidateAllEmpty(command);

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.Select(error => error.PropertyName).Distinct().Count()
            .ShouldBe(IdentifiersOf(command));
    }

    // --- What they deliberately leave to someone else --------------------------------------------

    [Theory]
    [InlineData(typeof(CheckOutCopyCommandValidator))]
    [InlineData(typeof(ReturnCopyCommandValidator))]
    [InlineData(typeof(RenewLoanCommandValidator))]
    [InlineData(typeof(PlaceHoldCommandValidator))]
    [InlineData(typeof(CancelHoldCommandValidator))]
    public void EveryValidator_ChecksTheShapeAndNothingElse(Type validator)
    {
        // Standing, the cap, lendability, the queue: every real refusal of a desk moment is
        // state, spans other aggregates or other contexts, and is asked by the handler — where
        // the refusal can name what the librarian tells the member.
        validator.GetConstructors().Single().GetParameters().ShouldBeEmpty();
    }
}
