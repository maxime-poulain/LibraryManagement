using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.RecordCopyCondition;

/// <summary>
/// Checks the shape of a <see cref="RecordCopyConditionCommand"/>.
/// </summary>
public sealed class RecordCopyConditionCommandValidator
    : AbstractValidator<RecordCopyConditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordCopyConditionCommandValidator"/> class.
    /// </summary>
    public RecordCopyConditionCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");

        // An enum arriving from outside carries whatever integer the caller sent, so a value the
        // type does not define reaches here perfectly typed. The domain would store it and every
        // reader downstream would have to guess what it meant.
        RuleFor(command => command.Condition)
            .IsInEnum()
            .WithMessage("A condition must be one of good, worn or damaged.");
    }
}
