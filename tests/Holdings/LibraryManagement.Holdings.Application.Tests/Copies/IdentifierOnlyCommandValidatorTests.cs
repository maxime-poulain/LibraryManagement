using FluentValidation.Results;
using LibraryManagement.Holdings.Application.Copies.DeclareCopyLost;
using LibraryManagement.Holdings.Application.Copies.FindCopy;
using LibraryManagement.Holdings.Application.Copies.ReleaseCopyForLending;
using LibraryManagement.Holdings.Application.Copies.RestrictCopyToReference;
using LibraryManagement.Holdings.Application.Copies.ReturnCopyFromRepair;
using LibraryManagement.Holdings.Application.Copies.SendCopyForRepair;
using LibraryManagement.Holdings.Application.Copies.WithdrawCopy;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

/// <summary>
/// Seven commands whose whole shape is one identifier, tested together rather than in seven files
/// saying the same three things. One file per validator is the convention where validators
/// differ; these are deliberately identical, and seven copies of a test would drift the way any
/// seven copies do.
/// </summary>
public sealed class IdentifierOnlyCommandValidatorTests
{
    /// <summary>
    /// Each case: the validator applied to its command, built around the one identifier.
    /// </summary>
    public static TheoryData<string> Commands => new(
        nameof(DeclareCopyLostCommand),
        nameof(FindCopyCommand),
        nameof(ReleaseCopyForLendingCommand),
        nameof(RestrictCopyToReferenceCommand),
        nameof(ReturnCopyFromRepairCommand),
        nameof(SendCopyForRepairCommand),
        nameof(WithdrawCopyCommand));

    private static ValidationResult Validate(string command, Guid copyId) => command switch
    {
        nameof(DeclareCopyLostCommand)
            => new DeclareCopyLostCommandValidator().Validate(new DeclareCopyLostCommand(copyId)),
        nameof(FindCopyCommand)
            => new FindCopyCommandValidator().Validate(new FindCopyCommand(copyId)),
        nameof(ReleaseCopyForLendingCommand)
            => new ReleaseCopyForLendingCommandValidator()
                .Validate(new ReleaseCopyForLendingCommand(copyId)),
        nameof(RestrictCopyToReferenceCommand)
            => new RestrictCopyToReferenceCommandValidator()
                .Validate(new RestrictCopyToReferenceCommand(copyId)),
        nameof(ReturnCopyFromRepairCommand)
            => new ReturnCopyFromRepairCommandValidator()
                .Validate(new ReturnCopyFromRepairCommand(copyId)),
        nameof(SendCopyForRepairCommand)
            => new SendCopyForRepairCommandValidator().Validate(new SendCopyForRepairCommand(copyId)),
        nameof(WithdrawCopyCommand)
            => new WithdrawCopyCommandValidator().Validate(new WithdrawCopyCommand(copyId)),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void AWellFormedCommand_IsAccepted(string command)
    {
        Validate(command, Guid.CreateVersion7()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void TheEmptyIdentifier_IsRejected(string command)
    {
        var outcome = Validate(command, Guid.Empty);

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == "CopyId");
    }

    // --- What they deliberately leave to someone else --------------------------------------------

    [Theory]
    [InlineData(typeof(DeclareCopyLostCommandValidator))]
    [InlineData(typeof(FindCopyCommandValidator))]
    [InlineData(typeof(ReleaseCopyForLendingCommandValidator))]
    [InlineData(typeof(RestrictCopyToReferenceCommandValidator))]
    [InlineData(typeof(ReturnCopyFromRepairCommandValidator))]
    [InlineData(typeof(SendCopyForRepairCommandValidator))]
    [InlineData(typeof(WithdrawCopyCommandValidator))]
    public void EveryValidator_ChecksTheShapeAndNothingElse(Type validator)
    {
        // The identifier is the whole shape. Whether the copy exists, and whether its status
        // allows the operation — repairing one already in repair, finding one nobody lost — are
        // the handler's and the aggregate's refusals, which is where they can be named.
        validator.GetConstructors().Single().GetParameters().ShouldBeEmpty();
    }
}
