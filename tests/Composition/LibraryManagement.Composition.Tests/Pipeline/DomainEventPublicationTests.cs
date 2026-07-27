using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.Pipeline;

/// <summary>
/// Domain events through the whole thing: the real mediator, the real interceptor, a real database.
/// </summary>
/// <remarks>
/// What is being tested is the timing, not the delivery: that publication happens inside the save
/// rather than around it. That is the property the outbox will rest on — an event's row and the
/// change that raised it written by one call, or neither written — and no unit test can show it,
/// since it is a claim about a single round trip to a database.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class DomainEventPublicationTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        EchoedRegistrations.Reset();

        _provider = new ServiceCollection()
            .AddMediator()
            .AddSharedInfrastructure()
            .AddCatalogModule(options => options.UseSqlServer(sqlServer.ConnectionString))
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.Database.EnsureDeletedAsync(Token);
        await context.Database.EnsureCreatedAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private Task<Result> DispatchAsync(RegisterAuthorCommand command)
        => InScopeAsync(async services =>
            await services.GetRequiredService<ICommandDispatcher>().DispatchAsync(command, Token));

    private async Task<Author?> FindAsync(AuthorId authorId)
        => await InScopeAsync(async services =>
            await services.GetRequiredService<IAuthorRepository>().GetByIdAsync(authorId, Token));

    [Fact]
    public async Task ACommandThatSucceeds_PublishesWhatItsAggregateRaised()
    {
        await DispatchAsync(new RegisterAuthorCommand(Guid.CreateVersion7(), "Ndiaye, Marie", 1967, null));

        EchoedRegistrations.Seen.ShouldHaveSingleItem().Value.ShouldBe("Ndiaye, Marie");
    }

    [Fact]
    public async Task ACommandThatFails_PublishesNothing()
    {
        // The domain refuses a year of death before the year of birth, so the handler reports a
        // failure, so the unit of work never saves, so the interceptor never runs. Publication is
        // tied to the write and not to the handler returning: an event announces something that
        // happened, and nothing did.
        await DispatchAsync(new RegisterAuthorCommand(Guid.CreateVersion7(), "Nobody, Real", 1944, 1900));

        EchoedRegistrations.Seen.ShouldBeEmpty();
    }

    [Fact]
    public async Task AHandlersOwnChanges_AreWrittenByTheSameSaveAsTheCommandThatCausedThem()
    {
        // True for as long as handlers run in process, and not the reason publication comes first —
        // the outbox is. The handler registers a second author while the first is still only
        // tracked, and both rows appear from one SaveChangesAsync. Publish after the write instead
        // and this author is never written at all: the unit of work behavior has already run.
        //
        // Delete this test when the outbox lands. An event drained by Hangfire is handled later, in
        // a transaction of its own, and a handler will have nothing left to join.
        await DispatchAsync(new RegisterAuthorCommand(
            Guid.CreateVersion7(),
            EchoedRegistrations.Echo + ", Testable",
            null,
            null));

        var companion = EchoedRegistrations.Companion.ShouldNotBeNull();

        (await FindAsync(companion)).ShouldNotBeNull();
    }

    [Fact]
    public async Task AHandlersOwnChanges_AreStampedLikeEveryOther()
    {
        // Which is what decides the order of the two interceptors. The audit one walks the change
        // tracker, and the companion is not in it until the handler has run — so stamping first
        // would write this row with a creation date of 0001-01-01, and nothing anywhere would fail
        // to mention it.
        await DispatchAsync(new RegisterAuthorCommand(
            Guid.CreateVersion7(),
            EchoedRegistrations.Echo + ", Stamped",
            null,
            null));

        var companion = await FindAsync(EchoedRegistrations.Companion.ShouldNotBeNull());

        companion.ShouldNotBeNull().CreatedOn.ShouldNotBe(default);
    }

    [Fact]
    public async Task HandlersThatFeedEachOther_AreReportedRatherThanLoopingForever()
    {
        // The other side of publishing first: handlers raise events of their own, so the set is not
        // known in advance and the loop that drains it has no natural end. This one answers its own
        // event, which is the smallest version of two handlers answering each other. A bounded loop
        // turns that into a stack trace; an unbounded one turns it into a request nobody ever gets
        // an answer to.
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await DispatchAsync(new RegisterAuthorCommand(
                Guid.CreateVersion7(),
                EchoedRegistrations.Loop + ", Forever",
                null,
                null)));

        thrown.Message.ShouldContain("rounds of publication");
    }
}

/// <summary>
/// Records what it was handed, and — for two marked names only — changes an aggregate of its own.
/// </summary>
/// <remarks>
/// The markers keep it inert for every other test in this project. A handler that registered a
/// companion author each time anyone registered one would quietly double the rows underneath tests
/// that are about something else entirely.
/// </remarks>
public sealed class EchoingAuthorHandler(IAuthorRepository authors) : IDomainEventHandler<AuthorRegistered>
{
    public ValueTask Handle(AuthorRegistered notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        EchoedRegistrations.Seen.Add(notification.AuthorizedName);
        var name = notification.AuthorizedName.Value;

        if (name.StartsWith(EchoedRegistrations.Echo, StringComparison.Ordinal))
        {
            // Under a name that carries no marker, so the companion's own registration — published
            // in the round after this one — is answered by nobody and the loop reaches its end.
            EchoedRegistrations.Companion = Register(EchoedRegistrations.CompanionName);
        }
        else if (name.StartsWith(EchoedRegistrations.Loop, StringComparison.Ordinal))
        {
            // Under the same marked name, so the next round answers it in turn. Deliberately the
            // thing the drain loop's bound exists to stop.
            Register(name);
        }

        return ValueTask.CompletedTask;
    }

    private AuthorId Register(string name)
    {
        var author = Author.Register(
            AuthorId.Generate(),
            PersonName.Create(name).Match(
                personName => personName,
                errors => throw new InvalidOperationException(errors[0].ToString())),
            LifeYears.Unknown);

        authors.Add(author);

        return author.Id;
    }
}

/// <summary>
/// What the handler saw, read back by the test that caused it. The tests sharing this run one at a
/// time, on the collection that also serialises access to the database.
/// </summary>
public static class EchoedRegistrations
{
    /// <summary>A registration the handler answers by registering one further author.</summary>
    public const string Echo = "Echo";

    /// <summary>A registration the handler answers with another of the same kind, endlessly.</summary>
    public const string Loop = "Loop";

    /// <summary>The name the companion is registered under, carrying neither marker.</summary>
    public const string CompanionName = "Written by a handler";

    public static List<PersonName> Seen { get; } = [];

    public static AuthorId? Companion { get; set; }

    public static void Reset()
    {
        Seen.Clear();
        Companion = null;
    }
}
