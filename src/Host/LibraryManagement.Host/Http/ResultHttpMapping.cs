using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Host.Http;

/// <summary>
/// Turns a <see cref="Result"/> into an HTTP response.
/// </summary>
/// <remarks>
/// <para>
/// The one place the two vocabularies meet. A module answers in error codes it owns; a caller reads
/// status codes it did not choose, and the translation between them belongs here rather than in
/// each endpoint — which is what keeps a refusal from being classified twice, differently.
/// </para>
/// <para>
/// <strong>A refused command is 422; only a query can be a 404.</strong> That split is the one
/// decision here, and it is made by the <em>shape</em> of the call rather than by reading the error
/// codes. A command is posted to a route naming an act — <c>/catalog/works/credit-author</c> — and
/// that route exists whether or not the author does, so a missing referent is the domain refusing,
/// not the address being wrong. A query addresses a thing, so a missing one is exactly a 404.
/// </para>
/// <para>
/// The alternative was to read <c>…NotFound</c> off the codes and answer 404 wherever they appeared.
/// It cannot work: <c>Catalog.AuthorNotFound</c> means <em>the author you are renaming</em> in one
/// handler and <em>the author you are crediting</em> in another, and the mapper has no way to tell
/// which — so it would answer 404 to a request whose address was perfectly good. The codes travel in
/// the body for a caller that wants them; the status says only what the protocol can know.
/// </para>
/// <para>
/// Flattening every refusal to 400 was rejected too: it tells a caller to fix a request that is not
/// wrong.
/// </para>
/// <para>
/// The body is a problem document carrying <em>every</em> error, not only the first. Validation
/// refuses a command with all its faults at once, on purpose, and a mapper that surfaced one would
/// make a caller replay the form to find the rest.
/// </para>
/// </remarks>
internal static class ResultHttpMapping
{
    /// <summary>The response for a command: nothing to return, so nothing is returned.</summary>
    /// <param name="result">What the command answered.</param>
    /// <returns>204 when it succeeded, a problem document when it did not.</returns>
    /// <remarks>
    /// 204 rather than 201: a command answers <see cref="Result"/> with no value, and inventing a
    /// created-resource location would promise a GET that does not exist. The caller minted the
    /// identifier it sent, so it already knows where the thing is when a route for it appears.
    /// </remarks>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Match(Results.NoContent, errors => ToProblem(errors, canBeMissing: false));
    }

    /// <summary>The response for a query.</summary>
    /// <typeparam name="TValue">What the query answers with.</typeparam>
    /// <param name="result">What the query answered.</param>
    /// <returns>200 and the value, or a problem document.</returns>
    public static IResult ToHttpResult<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Match(value => Results.Ok(value), errors => ToProblem(errors, canBeMissing: true));
    }

    private static IResult ToProblem(IReadOnlyErrorCollection errors, bool canBeMissing)
        => Results.Problem(
            statusCode: StatusCodeFor(errors, canBeMissing),
            title: errors[0].ErrorCode.Value,
            detail: errors[0].ErrorMessage,
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = errors
                    .Select(error => new
                    {
                        code = error.ErrorCode.Value,
                        message = error.ErrorMessage,
                        target = error.Target,
                    })
                    .ToArray(),
            });

    private static int StatusCodeFor(IReadOnlyErrorCollection errors, bool canBeMissing)
    {
        // Validation first: it runs before anything else in the pipeline, so a result carrying it
        // carries nothing the domain said — the command never reached a handler.
        if (errors.Any(error => error.ErrorCode == SharedErrorCodes.ValidationFailed))
        {
            return StatusCodes.Status400BadRequest;
        }

        if (errors.Any(error => error.ErrorCode == SharedErrorCodes.ConcurrencyConflict))
        {
            return StatusCodes.Status409Conflict;
        }

        // Every code, not any: a query refused partly because the thing is missing and partly for
        // another reason is a refusal, and answering 404 would say the whole address was wrong.
        return canBeMissing
            && errors.All(error => error.ErrorCode.Value.EndsWith("NotFound", StringComparison.Ordinal))
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;
    }
}
