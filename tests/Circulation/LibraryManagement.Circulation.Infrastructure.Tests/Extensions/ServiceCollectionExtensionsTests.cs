using LibraryManagement.Circulation.Application.Loans.RenewLoan;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Extensions;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Circulation.Infrastructure.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    private static IServiceCollection Registered()
        => new ServiceCollection().AddCirculationModule(options => options.UseSqlServer("Server=unused"));

    [Fact]
    public void AddCirculationModule_RegistersTheModulesOwnStore()
    {
        Registered().ShouldContain(service => service.ServiceType == typeof(CirculationDbContext));
    }

    [Theory]
    [InlineData(typeof(ILoanRepository), typeof(LoanRepository))]
    [InlineData(typeof(IHoldQueueRepository), typeof(HoldQueueRepository))]
    public void AddCirculationModule_RegistersTheDomainPorts(Type port, Type adapter)
    {
        var descriptor = Registered().Single(service => service.ServiceType == port);

        descriptor.ImplementationType.ShouldBe(adapter);
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddCirculationModule_RegistersThePolicyTheLibraryDecided()
    {
        var descriptor = Registered()
            .Single(service => service.ServiceType == typeof(CirculationPolicy));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        descriptor.ImplementationInstance.ShouldBe(CirculationPolicy.Current);
    }

    [Fact]
    public void AddCirculationModule_RegistersItsUnitOfWorkKeyedByItsOwnCommands()
    {
        var descriptor = Registered()
            .Single(service => service.ServiceType == typeof(IUnitOfWork));

        descriptor.IsKeyedService.ShouldBeTrue();
        descriptor.ServiceKey.ShouldBe(typeof(RenewLoanCommand).Assembly);
        descriptor.KeyedImplementationType.ShouldBe(typeof(CirculationUnitOfWork));
    }

    [Fact]
    public void ACirculationCommand_ResolvesTheCirculationUnitOfWork()
    {
        // End to end through the container, which is where the mechanism either works or does not
        // — the fourth keyed registration of the one interface.
        using var provider = new ServiceCollection()
            .AddSharedInfrastructure()
            .AddCirculationModule(options => options.UseSqlServer("Server=unused"))
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IUnitOfWorkResolver>();

        resolver.Resolve(new RenewLoanCommand(Guid.NewGuid()))
            .ShouldBeOfType<CirculationUnitOfWork>();
    }
}
