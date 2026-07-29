using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Tests;

// Builders that unwrap the Results these tests are not about, so each test reads as the one thing
// it asserts rather than as a chain of matches.
internal static class Catalogue
{
    public static PersonName Name(string value) => Unwrap(PersonName.Create(value));

    public static Title TitleOf(string value) => Unwrap(Title.Create(value));

    public static Isbn AnIsbn(string value = "9782070612758") => Unwrap(Isbn.Create(value));

    public static LifeYears Years(int? birth, int? death) => Unwrap(LifeYears.Create(birth, death));

    public static Author AnAuthor(string name = "Saint-Exupéry, Antoine de")
        => Author.Register(AuthorId.Generate(), Name(name), LifeYears.Unknown);

    public static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    public static IReadOnlyErrorCollection ErrorsOf<T>(Result<T> result)
        => result.Match(_ => throw new InvalidOperationException("Expected a failure."), errors => errors);

    public static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static T Unwrap<T>(Result<T> result)
        => result.Match(value => value, errors => throw new InvalidOperationException(errors[0].ToString()));
}
