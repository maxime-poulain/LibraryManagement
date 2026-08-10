using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Application.Works.GetWorkById;
using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Migrations.SqlServer;
using LibraryManagement.Composition.Tests.Logging;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraryManagement.Composition.Tests.Pipeline;

/// <summary>
/// A command and a query through the whole thing: the real mediator, the real behaviors, the real
/// module, a real SQL Server.
/// </summary>
/// <remarks>
/// Nothing exercised this before. The dispatchers were tested against a fake sender, the behaviors
/// against a fake next, the repositories against a database with no pipeline in front of them —
/// each half proven, the seam between them assumed.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CatalogPipelineTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly RecordedLogs _logs = new();

    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        // The mediator, its pipeline and its scoped lifetime come from the assembly's one
        // AddMediator call — see CompositionRoot.
        _provider = CompositionRoot.Services()
            // Only the solution's own lines reach the recorder: EF Core narrates every SQL command
            // at Information, and these tests assert our narration, not its.
            .AddLogging(logging => logging
                .AddProvider(_logs)
                .AddFilter<RecordedLogs>((category, _) =>
                    category?.StartsWith("LibraryManagement", StringComparison.Ordinal) == true))
            .AddCatalogModule(options => options.UseCatalogSqlServer(sqlServer.ConnectionString))
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.Database.EnsureDeletedAsync(Token);
        await context.Database.MigrateAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private static RegisterAuthorCommand ARegistration(string name = "Ernaux, Annie")
        => new(Guid.CreateVersion7(), name, 1940, null);

    private async Task<Author?> FindAsync(Guid authorId)
        => await InScopeAsync(async services =>
        {
            var repository = services.GetRequiredService<IAuthorRepository>();
            return await repository.GetByIdAsync(AuthorId.Create(authorId), Token);
        });

    // --- A well-formed command is handled, and written -----------------------------------------------

    [Fact]
    public async Task AValidCommand_Succeeds()
    {
        var command = ARegistration();

        var result = await InScopeAsync(async services =>
            await services.GetRequiredService<ICommandDispatcher>().DispatchAsync(command, Token));

        result.Match(() => "success", errors => errors[0].ToString()).ShouldBe("success");
    }

    [Fact]
    public async Task AValidCommand_ReachesTheDatabase()
    {
        // Nothing in the handler saves. The unit of work behavior does, after the handler reported
        // success — and this is the first test that proves the two are actually connected.
        var command = ARegistration("Deleuze, Gilles");

        await InScopeAsync(async services =>
            await services.GetRequiredService<ICommandDispatcher>().DispatchAsync(command, Token));

        var author = await FindAsync(command.AuthorId);

        author.ShouldNotBeNull();
        author.PreferredName.Value.ShouldBe("Deleuze, Gilles");
    }

    // --- A malformed command is stopped before it can write ------------------------------------------

    [Fact]
    public async Task AnInvalidCommand_FailsWithAValidationError()
    {
        var command = ARegistration(name: "   ");

        var result = await InScopeAsync(async services =>
            await services.GetRequiredService<ICommandDispatcher>().DispatchAsync(command, Token));

        result.Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode)
            .ShouldBe(SharedErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task AnInvalidCommand_WritesNothing()
    {
        // The order of the two behaviors, observed from the outside: validation runs first, so the
        // unit of work is never reached. Register them the other way round and this is the test
        // that notices.
        var command = ARegistration(name: "");

        await InScopeAsync(async services =>
            await services.GetRequiredService<ICommandDispatcher>().DispatchAsync(command, Token));

        (await FindAsync(command.AuthorId)).ShouldBeNull();
    }

    [Fact]
    public async Task ACommandTheDomainRefuses_WritesNothing()
    {
        // Past the validator, refused by the domain: a year of death before the year of birth. The
        // handler reports a failure and the unit of work leaves the store untouched — the guarantee
        // the explicit transaction used to provide.
        var command = new RegisterAuthorCommand(Guid.CreateVersion7(), "Someone, Real", 1944, 1900);

        var result = await InScopeAsync(async services =>
            await services.GetRequiredService<ICommandDispatcher>().DispatchAsync(command, Token));

        result.Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode)
            .ShouldBe(CatalogErrorCodes.InvalidLifeYears);
        (await FindAsync(command.AuthorId)).ShouldBeNull();
    }

    // --- A query goes through validation and nothing else --------------------------------------------

    [Fact]
    public async Task AQueryForSomethingAbsent_FailsWithNotFound()
    {
        var result = await InScopeAsync(async services =>
            await services.GetRequiredService<IQueryDispatcher>()
                .DispatchAsync(new GetWorkByIdQuery(Guid.CreateVersion7()), Token));

        result.Match<ErrorCode?>(_ => null, errors => errors[0].ErrorCode)
            .ShouldBe(CatalogErrorCodes.WorkNotFound);
    }

    // --- Every outcome is a line in the log ----------------------------------------------------------

    [Fact]
    public async Task ACommandValidationRefuses_IsStillALineInTheLog()
    {
        // Logging is the outermost behavior, which is exactly what this asserts: register it
        // inside validation and the refusal returns before the logger ever runs — and this is the
        // test that notices. The line names the command and the code, never the fields.
        await InScopeAsync(async services =>
            await services.GetRequiredService<ICommandDispatcher>()
                .DispatchAsync(ARegistration(name: "   "), Token));

        var line = _logs.Lines.ShouldHaveSingleItem();
        line.Category.ShouldBe(typeof(RegisterAuthorCommand).FullName);
        line.Level.ShouldBe(LogLevel.Warning);
        line.Message.ShouldContain(nameof(RegisterAuthorCommand));
        line.Message.ShouldContain(SharedErrorCodes.ValidationFailed.Value);
    }

    [Fact]
    public async Task ACommandThatSucceeds_IsAnInformationLine()
    {
        var command = ARegistration("Perec, Georges");

        await InScopeAsync(async services =>
            await services.GetRequiredService<ICommandDispatcher>().DispatchAsync(command, Token));

        var line = _logs.Lines.ShouldHaveSingleItem();
        line.Level.ShouldBe(LogLevel.Information);

        // The line carries the command's name and never its contents: what an employee typed into
        // a form has no business in a log file.
        line.Message.ShouldContain(nameof(RegisterAuthorCommand));
        line.Message.ShouldNotContain("Perec");
    }

    [Fact]
    public async Task AMalformedQuery_FailsValidationRatherThanReportingNotFound()
    {
        // The distinction the query validator exists to draw: "you asked badly" is not "there is no
        // such work". Before it existed, the handler answered WorkNotFound for both.
        var result = await InScopeAsync(async services =>
            await services.GetRequiredService<IQueryDispatcher>()
                .DispatchAsync(new GetWorkByIdQuery(Guid.Empty), Token));

        result.Match<ErrorCode?>(_ => null, errors => errors[0].ErrorCode)
            .ShouldBe(SharedErrorCodes.ValidationFailed);
    }
}
