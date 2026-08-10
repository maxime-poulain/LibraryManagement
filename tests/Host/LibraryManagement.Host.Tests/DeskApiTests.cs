using System.Net;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Host.Auditing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// The desk's API over the wire: a request in, a command dispatched, a status out.
/// </summary>
/// <remarks>
/// Deliberately narrow. The pipeline is already proven by the composition tests and the mapping by
/// <see cref="ResultHttpMappingTests"/>; what is new here is the wiring between them — that a route
/// exists, that the body binds onto a command record, that the audit header reaches the columns, and
/// that a module's refusal arrives as the status the mapper promises.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DeskApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TheHost _host = null!;
    private HttpClient _client = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_DeskApi_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await Databases.DropAsync(ConnectionString, Token);
        _host = new TheHost(ConnectionString);
        _client = _host.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _host.DisposeAsync();
    }

    private Task<HttpResponseMessage> PostAsync(string route, object body)
        => _client.PostAsJsonAsync(route, body, Token);

    [Fact]
    public async Task ACommand_ArrivesAsItsRecordAndAnswersNoContent()
    {
        var authorId = Guid.CreateVersion7();

        var response = await PostAsync(
            "/catalog/authors",
            new { AuthorId = authorId, PreferredName = "Ernaux, Annie", BirthYear = 1940, DeathYear = (int?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Read back through the store, because a status code proves the pipeline answered and not
        // that anything was written.
        var stored = await _host.InScopeAsync(services => services
            .GetRequiredService<CatalogDbContext>()
            .Set<Author>()
            .SingleOrDefaultAsync(author => author.Id == AuthorId.Create(authorId), Token));

        stored.ShouldNotBeNull().LifeYears.Birth.ShouldBe(1940);
    }

    [Fact]
    public async Task AQuery_AnswersOkWithItsDto()
    {
        var workId = Guid.CreateVersion7();
        (await PostAsync("/catalog/works", new { WorkId = workId, PreferredTitle = "Les Années", AuthorIds = Array.Empty<Guid>() }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await _client.GetAsync(new Uri($"/catalog/works/{workId}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("Les Années");
    }

    [Fact]
    public async Task AMalformedCommand_AnswersBadRequestWithTheValidationCode()
    {
        var response = await PostAsync(
            "/catalog/authors",
            new { AuthorId = Guid.CreateVersion7(), PreferredName = "", BirthYear = (int?)null, DeathYear = (int?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("Shared.ValidationFailed");
    }

    [Fact]
    public async Task AQueryForSomethingAbsent_AnswersNotFound()
    {
        var response = await _client.GetAsync(
            new Uri($"/catalog/works/{Guid.CreateVersion7()}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ARefusedCommand_AnswersUnprocessable()
    {
        // Understood, well-formed, and addressed to a work that exists — refused because the author
        // it credits does not. Neither a syntax problem nor a missing address.
        var workId = Guid.CreateVersion7();
        (await PostAsync("/catalog/works", new { WorkId = workId, PreferredTitle = "Les Années", AuthorIds = Array.Empty<Guid>() }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await PostAsync(
            "/catalog/works/credit-author",
            new { WorkId = workId, AuthorId = Guid.CreateVersion7() });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("Catalog.AuthorNotFound");
    }

    [Fact]
    public async Task TheEmployeeHeader_ReachesTheAuditColumns()
    {
        var authorId = Guid.CreateVersion7();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/catalog/authors")
        {
            Content = JsonContent.Create(new
            {
                AuthorId = authorId,
                PreferredName = "Gary, Romain",
                BirthYear = (int?)1914,
                DeathYear = (int?)1980,
            }),
        };
        request.Headers.Add(EmployeeHeaderMiddleware.HeaderName, "desk-07");

        (await _client.SendAsync(request, Token)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var createdBy = await _host.InScopeAsync(services => services
            .GetRequiredService<CatalogDbContext>()
            .Set<Author>()
            .Where(author => author.Id == AuthorId.Create(authorId))
            .Select(author => EF.Property<string?>(author, "CreatedBy"))
            .SingleAsync(Token));

        createdBy.ShouldBe("desk-07");
    }

    [Fact]
    public async Task WithoutTheHeader_TheChangeIsAuditedAsNobodys()
    {
        // Not refused: nothing here authenticates anybody, and a gate that verified nothing would
        // only look like one. The columns record the truth instead.
        var authorId = Guid.CreateVersion7();

        (await PostAsync(
            "/catalog/authors",
            new { AuthorId = authorId, PreferredName = "Ajar, Émile", BirthYear = (int?)null, DeathYear = (int?)null }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var createdBy = await _host.InScopeAsync(services => services
            .GetRequiredService<CatalogDbContext>()
            .Set<Author>()
            .Where(author => author.Id == AuthorId.Create(authorId))
            .Select(author => EF.Property<string?>(author, "CreatedBy"))
            .SingleAsync(Token));

        createdBy.ShouldBeNull();
    }

    [Fact]
    public async Task AScheduledCommand_HasNoRoute()
    {
        // The seven the daily process owns are not desk acts, and neither are the reactions one
        // module dispatches for another. A route would let a request assert a fact its announcer
        // never made.
        var response = await PostAsync("/circulation/holds/cancel-for-debt", new { BorrowerId = Guid.CreateVersion7() });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
