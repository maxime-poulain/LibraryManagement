using FluentValidation;

namespace LibraryManagement.Circulation.Application.Holds.MergeHoldQueues;

/// <summary>
/// Checks the shape of a <see cref="MergeHoldQueuesCommand"/>.
/// </summary>
public sealed class MergeHoldQueuesCommandValidator : AbstractValidator<MergeHoldQueuesCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MergeHoldQueuesCommandValidator"/> class.
    /// </summary>
    public MergeHoldQueuesCommandValidator()
    {
        RuleFor(command => command.AbsorbedEditionId)
            .NotEmpty()
            .WithMessage("An absorbed edition identifier is required.");

        RuleFor(command => command.SurvivingEditionId)
            .NotEmpty()
            .WithMessage("A surviving edition identifier is required.")
            // Catalog refuses a record merged into itself before anything is announced, and the
            // aggregate refuses a queue absorbing itself. Checked here too because a command answers
            // for its own shape.
            .NotEqual(command => command.AbsorbedEditionId)
            .WithMessage("A record cannot survive its own merge.");
    }
}
