using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Holdings.Application.Copies.AcquireCopy;
using LibraryManagement.Holdings.Domain;
using LibraryManagement.Holdings.Infrastructure.Extensions;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.Pipeline;

/// <summary>
/// Two modules in one container, which nothing had ever exercised.
/// </summary>
/// <remarks>
/// <para>
/// Every mechanism this file touches was built for five modules and had only ever seen one.
/// <c>ModuleUnitOfWorkResolver</c> is keyed by the assembly declaring a command precisely so that
/// five registrations of one interface do not answer for each other — and with a single module,
/// resolving by type would have passed every test in the suite just as well. Two schemas, two
/// outbox tables and two drains are the same story.
/// </para>
/// <para>
/// It is also the first time a command's precondition leaves its own context: acquiring a copy asks
/// Catalog's published language whether the edition exists, through a container where both modules
/// are registered and neither knows the other's store.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TwoModulesTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    // A database of this class's own, on the same server. The collection's other classes share one
    // and rebuild it as they go; a class that both drops a database and expects two modules' tables
    // in it cannot join that arrangement without breaking the neighbours.
    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_TwoModules_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        // One host, both modules. Holdings deliberately does not register Catalog for the caller:
        // deciding the composition is the host's business.
        _provider = CompositionRoot.Services()
            .AddCatalogModule(options => options.UseSqlServer(ConnectionString))
            .AddHoldingsModule(options => options.UseSqlServer(ConnectionString))
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalog.Database.EnsureDeletedAsync(Token);
        await catalog.Database.EnsureCreatedAsync(Token);

        // Not EnsureCreated: that one creates the database, and answers "already there" for a second
        // context over the same one — leaving this module's schema unbuilt and every query against
        // it failing on a name the model insists exists. The relational creator builds the tables of
        // the model in hand and nothing else, which is exactly what a second module needs. It is
        // also the first sign that one database per solution and one context per module do not
        // compose for free; a host will meet the same seam and answer it with migrations.
        var holdings = scope.ServiceProvider.GetRequiredService<HoldingsDbContext>();
        await ((IRelationalDatabaseCreator)holdings.Database.GetService<IDatabaseCreator>())
            .CreateTablesAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private Task<Result> DispatchAsync(ICommand<Result> command)
        => InScopeAsync(services => services
            .GetRequiredService<ICommandDispatcher>()
            .DispatchAsync(command, Token)
            .AsTask());

    private async Task<Guid> ACatalogedEditionAsync()
    {
        var workId = Guid.CreateVersion7();
        (await DispatchAsync(new RegisterWorkCommand(workId, "La Horde du Contrevent", [])))
            .HasErrors().ShouldBeFalse();

        var editionId = Guid.CreateVersion7();
        (await DispatchAsync(new RegisterEditionCommand(editionId, workId, null)))
            .HasErrors().ShouldBeFalse();

        return editionId;
    }

    private static AcquireCopyCommand AnAcquisition(Guid editionId, string barcode)
        => new(Guid.CreateVersion7(), editionId, barcode, "843.912 SAI", new DateOnly(2024, 3, 14));

    [Fact]
    public async Task EachModulesCommand_IsWrittenThroughItsOwnStore()
    {
        // The defect the keyed resolver exists to prevent: with a resolution by type, the module
        // registered last would answer for both, and one of these two would silently write nothing.
        var editionId = await ACatalogedEditionAsync();
        var acquisition = AnAcquisition(editionId, "30124000512");

        (await DispatchAsync(acquisition)).HasErrors().ShouldBeFalse();

        await using var scope = _provider.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        (await catalog.Set<Catalog.Domain.Editions.Edition>().CountAsync(Token)).ShouldBe(1);

        var holdings = scope.ServiceProvider.GetRequiredService<HoldingsDbContext>();
        (await holdings.Set<Holdings.Domain.Copies.Copy>().CountAsync(Token)).ShouldBe(1);
    }

    [Fact]
    public async Task AcquiringACopyOfAnEditionNobodyCataloged_IsRefusedAcrossTheBoundary()
    {
        // Holdings holds an EditionId and no foreign key backs it, so nothing but this question
        // stands between the collection and a copy of something that does not exist.
        var outcome = await DispatchAsync(
            AnAcquisition(Guid.CreateVersion7(), "30124000777"));

        outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode))
            .ShouldContain(HoldingsErrorCodes.EditionNotFound);
    }

    [Fact]
    public async Task EachModule_KeepsItsOwnOutbox()
    {
        // One table per module, in each module's own schema: a row must be written by the same save
        // as the change that raised it, and a central table would be a second context and a second
        // transaction.
        var editionId = await ACatalogedEditionAsync();
        (await DispatchAsync(AnAcquisition(editionId, "30124000888"))).HasErrors().ShouldBeFalse();

        await using var scope = _provider.CreateAsyncScope();

        var catalogRows = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM catalog.OutboxMessage")
            .SingleAsync(Token);

        var holdingsRows = await scope.ServiceProvider.GetRequiredService<HoldingsDbContext>()
            .Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM holdings.OutboxMessage")
            .SingleAsync(Token);

        // The work and the edition on one side, the copy on the other. Neither table saw the
        // other's events.
        catalogRows.ShouldBe(2);
        holdingsRows.ShouldBe(1);
    }

    [Fact]
    public async Task TheLendabilityPort_AnswersForACopyThePipelineJustCreated()
    {
        // End to end through the published language: a command writes, and the surface Circulation
        // will one day call reads the result back.
        var editionId = await ACatalogedEditionAsync();
        var acquisition = AnAcquisition(editionId, "30124000999");

        (await DispatchAsync(acquisition)).HasErrors().ShouldBeFalse();

        var answer = await InScopeAsync(services => services
            .GetRequiredService<ICopyLendability>()
            .OfAsync(acquisition.CopyId, Token)
            .AsTask());

        answer.ShouldBe(Lendability.Lendable);
    }
}
