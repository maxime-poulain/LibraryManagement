using System.Text.Json.Serialization;
using LibraryManagement.Holdings.Application.Copies.WithdrawCopy;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Extensions;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Holdings.Infrastructure.Serialization;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Holdings.Infrastructure.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    private static IServiceCollection Registered()
        => new ServiceCollection().AddHoldingsModule(options => options.UseSqlServer("Server=unused"));

    [Fact]
    public void AddHoldingsModule_RegistersTheModulesOwnStore()
    {
        Registered().ShouldContain(service => service.ServiceType == typeof(HoldingsDbContext));
    }

    [Theory]
    [InlineData(typeof(ICopyRepository), typeof(CopyRepository))]
    [InlineData(typeof(ICopyLendability), typeof(Infrastructure.PublishedLanguage.CopyLendability))]
    public void AddHoldingsModule_RegistersTheDomainAndPublishedPorts(Type port, Type adapter)
    {
        var descriptor = Registered().Single(service => service.ServiceType == port);

        descriptor.ImplementationType.ShouldBe(adapter);
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddHoldingsModule_RegistersItsUnitOfWorkKeyedByItsOwnCommands()
    {
        // The key is what keeps the modules' units of work apart: registered unkeyed, the last
        // module added would write every other module's changes through its own store.
        var descriptor = Registered()
            .Single(service => service.ServiceType == typeof(IUnitOfWork));

        descriptor.IsKeyedService.ShouldBeTrue();
        descriptor.ServiceKey.ShouldBe(typeof(WithdrawCopyCommand).Assembly);
        descriptor.KeyedImplementationType.ShouldBe(typeof(HoldingsUnitOfWork));
    }

    [Fact]
    public void AddHoldingsModule_ContributesEveryValueObjectsJsonConverter()
    {
        // The outbox serializer collects these; one forgotten here is an event that fails to
        // serialize on the first save that raises it.
        var converters = Registered()
            .Where(service => service.ServiceType == typeof(JsonConverter))
            .Select(service => service.ImplementationType)
            .ToList();

        converters.ShouldContain(typeof(BarcodeJsonConverter));
        converters.ShouldContain(typeof(ShelfmarkJsonConverter));
    }

    [Fact]
    public void AHoldingsCommand_ResolvesTheHoldingsUnitOfWork()
    {
        // End to end through the container, which is where the mechanism either works or does not.
        using var provider = new ServiceCollection()
            .AddSharedInfrastructure()
            .AddHoldingsModule(options => options.UseSqlServer("Server=unused"))
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IUnitOfWorkResolver>();

        resolver.Resolve(new WithdrawCopyCommand(Guid.NewGuid()))
            .ShouldBeOfType<HoldingsUnitOfWork>();
    }
}
