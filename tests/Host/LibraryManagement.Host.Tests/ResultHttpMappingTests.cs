using LibraryManagement.Host.Http;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// The one place a module's vocabulary becomes a caller's: error codes in, status codes out.
/// </summary>
/// <remarks>
/// No database and no host — this is a pure function over a <see cref="Result"/>, and it runs in the
/// pull-request gate for that reason. The rules it encodes are small enough to state and easy enough
/// to get wrong that stating them once, here, is worth more than the endpoints that depend on them.
/// </remarks>
public sealed class ResultHttpMappingTests
{
    private static readonly ErrorCode NotFound = new("Members.MemberNotFound");
    private static readonly ErrorCode Refusal = new("Circulation.LoanCapReached");

    private static int StatusOf(IResult result)
        => result switch
        {
            IStatusCodeHttpResult statused => statused.StatusCode
                ?? throw new InvalidOperationException("The mapper always sets a status."),
            _ => throw new InvalidOperationException($"Unexpected result: {result.GetType().Name}."),
        };

    private static ProblemDetails ProblemOf(IResult result)
        => result.ShouldBeAssignableTo<ProblemHttpResult>().ProblemDetails;

    [Fact]
    public void ASucceedingCommand_AnswersNoContent()
    {
        // Nothing to return, so nothing is returned — and inventing a created-resource location
        // would promise a GET that does not exist.
        StatusOf(Result.Success().ToHttpResult()).ShouldBe(StatusCodes.Status204NoContent);
    }

    [Fact]
    public void ASucceedingQuery_AnswersOkWithTheValue()
    {
        var result = Result<string>.Success("Le Petit Prince").ToHttpResult();

        StatusOf(result).ShouldBe(StatusCodes.Status200OK);
        result.ShouldBeAssignableTo<Ok<string>>().Value.ShouldBe("Le Petit Prince");
    }

    [Fact]
    public void AMalformedCommand_AnswersBadRequest()
    {
        var result = Result.Failure(SharedErrorCodes.ValidationFailed, "'Barcode' must not be empty.")
            .ToHttpResult();

        StatusOf(result).ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void AConcurrencyConflict_AnswersConflict()
    {
        var result = Result.Failure(SharedErrorCodes.ConcurrencyConflict, "Somebody else changed it.")
            .ToHttpResult();

        StatusOf(result).ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void AQueryForAMissingThing_AnswersNotFound()
    {
        // A query addresses a thing, so a missing one is exactly what 404 means.
        StatusOf(Result<string>.Failure(NotFound, "No member is enrolled under that identifier.")
                .ToHttpResult())
            .ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ACommandNamingAMissingThing_IsARefusalAndNotAMissingAddress()
    {
        // The route names an act and exists whether or not the author does, so the address was
        // never wrong — the domain refused. The mapper cannot read this off the code either:
        // Catalog.AuthorNotFound means the author being renamed in one handler and the author
        // being credited in another, and only the shape of the call tells them apart.
        StatusOf(Result.Failure(NotFound, "No author is cataloged under that identifier.").ToHttpResult())
            .ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void ADomainRefusal_AnswersUnprocessable()
    {
        // Understood, well-formed, addressed to something that exists — and refused. None of the
        // other three statuses says that, which is the whole reason this one is here.
        StatusOf(Result.Failure(Refusal, "This borrower already holds five loans.").ToHttpResult())
            .ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void AQueryRefusedOnlyPartlyForAMissingThing_IsNotANotFound()
    {
        // 404 would tell the caller the whole address was wrong, when half the reason was not.
        var result = Result<string>.Failure(
            [
                new Error(NotFound, "No member is enrolled under that identifier."),
                new Error(Refusal, "This borrower already holds five loans."),
            ]).ToHttpResult();

        StatusOf(result).ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void AValidationFailure_WinsOverEverythingElse()
    {
        // Validation runs before the handler, so a result carrying it carries nothing the domain
        // said — whatever else rode along cannot have come from a decision.
        var result = Result.Failure(
            [
                new Error(NotFound, "No member is enrolled under that identifier."),
                new Error(SharedErrorCodes.ValidationFailed, "'CardNumber' must not be empty."),
            ]).ToHttpResult();

        StatusOf(result).ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void TheProblemDocument_CarriesEveryErrorAndNotOnlyTheFirst()
    {
        // Validation refuses a command with all its faults at once, on purpose. A mapper that
        // surfaced one would make the caller replay the form to discover the rest.
        var result = Result.Failure(
            [
                new Error(SharedErrorCodes.ValidationFailed, "'GivenName' must not be empty.", "GivenName"),
                new Error(SharedErrorCodes.ValidationFailed, "'FamilyName' must not be empty.", "FamilyName"),
            ]).ToHttpResult();

        var problem = ProblemOf(result);

        problem.Title.ShouldBe("Shared.ValidationFailed");
        problem.Detail.ShouldBe("'GivenName' must not be empty.");
        problem.Extensions["errors"].ShouldNotBeNull()
            .ShouldBeAssignableTo<System.Collections.IEnumerable>()
            .Cast<object>().Count().ShouldBe(2);
    }
}
