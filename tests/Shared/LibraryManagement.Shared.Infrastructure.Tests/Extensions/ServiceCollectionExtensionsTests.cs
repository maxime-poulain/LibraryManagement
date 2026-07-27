using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Extensions;
using LibraryManagement.Shared.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSharedInfrastructure_RegistersTheCommandDispatcher()
    {
        var descriptor = new ServiceCollection()
            .AddSharedInfrastructure()
            .Single(service => service.ServiceType == typeof(ICommandDispatcher));

        descriptor.ImplementationType.ShouldBe(typeof(MediatorCommandDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersTheQueryDispatcher()
    {
        var descriptor = new ServiceCollection()
            .AddSharedInfrastructure()
            .Single(service => service.ServiceType == typeof(IQueryDispatcher));

        descriptor.ImplementationType.ShouldBe(typeof(MediatorQueryDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersTheCommandValidator()
    {
        var descriptor = new ServiceCollection()
            .AddSharedInfrastructure()
            .Single(service => service.ServiceType == typeof(ICommandValidator));

        descriptor.ImplementationType.ShouldBe(typeof(FluentValidationCommandValidator));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersNothingElse()
    {
        // A module owns its own DbContext, repositories, validators and ITransactionManager. Shared
        // infrastructure that registered anything module-specific would defeat the separation.
        new ServiceCollection().AddSharedInfrastructure().Count.ShouldBe(3);
    }

    [Fact]
    public void AddSharedInfrastructure_WithANullCollection_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddSharedInfrastructure());
    }
}
