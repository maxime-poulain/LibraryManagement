using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.RegisterAuthor;

/// <summary>
/// Handles <see cref="RegisterAuthorCommand"/>.
/// </summary>
/// <param name="authors">The store to add the new record to.</param>
public sealed class RegisterAuthorCommandHandler(IAuthorRepository authors)
    : ICommandHandler<RegisterAuthorCommand, Result>
{
    /// <inheritdoc/>
    public ValueTask<Result> Handle(
        RegisterAuthorCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Combine rather than chain: the name and the years are independent, so an employee who got
        // both wrong should be told both times rather than once per attempt.
        var result = NameForm.Create(command.AuthorizedName)
            .Combine(LifeYears.Create(command.BirthYear, command.DeathYear))
            .Bind(parts =>
            {
                var author = Author.Register(
                    AuthorId.Create(command.AuthorId),
                    parts.First,
                    parts.Second);

                authors.Add(author);

                // Nothing is written here. The unit of work behavior saves once this handler has
                // reported success, so "the command succeeded" and "the work was written" are one
                // event.
                return Result.Success();
            });

        return ValueTask.FromResult(result);
    }
}
