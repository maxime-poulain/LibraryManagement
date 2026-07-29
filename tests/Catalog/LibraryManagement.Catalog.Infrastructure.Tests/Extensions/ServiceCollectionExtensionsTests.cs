using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Infrastructure.Extensions;
using LibraryManagement.Shared.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    private static IServiceCollection Registered()
        => new ServiceCollection().AddCatalogModule(options => options.UseSqlServer("Server=unused"));

    [Fact]
    public void AddCatalogModule_RegistersTheModulesOwnStore()
    {
        Registered().ShouldContain(service => service.ServiceType == typeof(CatalogDbContext));
    }

    [Theory]
    [InlineData(typeof(IAuthorRepository), typeof(AuthorRepository))]
    [InlineData(typeof(IWorkRepository), typeof(WorkRepository))]
    public void AddCatalogModule_RegistersTheDomainAndReadPorts(Type port, Type adapter)
    {
        var descriptor = Registered().Single(service => service.ServiceType == port);

        descriptor.ImplementationType.ShouldBe(adapter);
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddCatalogModule_RegistersItsUnitOfWorkKeyedByItsOwnCommands()
    {
        // The key is what keeps five modules' units of work apart. Registered unkeyed, the last
        // module to be added would write every other module's changes through its own store.
        var descriptor = Registered()
            .Single(service => service.ServiceType == typeof(IUnitOfWork));

        descriptor.IsKeyedService.ShouldBeTrue();
        descriptor.ServiceKey.ShouldBe(typeof(RegisterAuthorCommand).Assembly);
        descriptor.KeyedImplementationType.ShouldBe(typeof(CatalogUnitOfWork));
    }

    // Building the module's store builds its interceptors, and neither needs a mediator anymore:
    // the outbox interceptor serializes instead of publishing, and delivery happens in the drain,
    // which nothing here ever runs.
    private static ServiceProvider ModuleInAContainer()
        => new ServiceCollection()
            .AddSharedInfrastructure()
            .AddCatalogModule(options => options.UseSqlServer("Server=unused"))
            .BuildServiceProvider();

    [Fact]
    public void ACatalogCommand_ResolvesTheCatalogUnitOfWork()
    {
        // End to end through the container, which is where the mechanism either works or does not.
        using var provider = ModuleInAContainer();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IUnitOfWorkResolver>();

        resolver.Resolve(new RegisterAuthorCommand(Guid.NewGuid(), "Anyone", null, null))
            .ShouldBeOfType<CatalogUnitOfWork>();
    }

    [Fact]
    public void AForeignCommand_FindsNoUnitOfWorkHere()
    {
        // A command from a module that has not registered one must fail loudly, not quietly borrow
        // the Catalog store.
        using var provider = ModuleInAContainer();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IUnitOfWorkResolver>();

        Should.Throw<InvalidOperationException>(() => resolver.Resolve(new ForeignCommand()));
    }

    [Fact]
    public void TheResolver_IsTheSharedOne()
    {
        new ServiceCollection()
            .AddSharedInfrastructure()
            .Single(service => service.ServiceType == typeof(IUnitOfWorkResolver))
            .ImplementationType
            .ShouldBe(typeof(ModuleUnitOfWorkResolver));
    }
}
