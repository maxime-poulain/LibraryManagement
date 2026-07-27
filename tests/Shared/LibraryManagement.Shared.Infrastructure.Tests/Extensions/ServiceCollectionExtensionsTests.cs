using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Extensions;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;
using LibraryManagement.Shared.Infrastructure.Transactions;
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
    public void AddSharedInfrastructure_RegistersTheTransactionManagerResolver()
    {
        var descriptor = new ServiceCollection()
            .AddSharedInfrastructure()
            .Single(service => service.ServiceType == typeof(ITransactionManagerResolver));

        descriptor.ImplementationType.ShouldBe(typeof(ModuleTransactionManagerResolver));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersNothingElse()
    {
        // A module owns its own DbContext, repositories, validators and ITransactionManager. Shared
        // infrastructure that registered anything module-specific would defeat the separation.
        new ServiceCollection().AddSharedInfrastructure().Count.ShouldBe(4);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersNoTransactionManagerOfItsOwn()
    {
        // The resolver is shared; the managers it finds are not. Shared infrastructure registering
        // one would give every module the same store.
        new ServiceCollection()
            .AddSharedInfrastructure()
            .ShouldNotContain(service => service.ServiceType == typeof(ITransactionManager));
    }

    [Fact]
    public void AddSharedInfrastructure_WithANullCollection_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddSharedInfrastructure());
    }

    [Fact]
    public void AddModuleTransactionManager_RegistersItKeyedByTheModule()
    {
        var descriptor = new ServiceCollection()
            .AddModuleTransactionManager<RecordingTransactionManager>(typeof(TestCommand).Assembly)
            .Single();

        descriptor.ServiceType.ShouldBe(typeof(ITransactionManager));
        descriptor.IsKeyedService.ShouldBeTrue();
        descriptor.ServiceKey.ShouldBe(typeof(TestCommand).Assembly);
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddModuleTransactionManager_RegistersNothingUnkeyed()
    {
        // An unkeyed registration is exactly the defect this whole mechanism exists to prevent: the
        // last module to register would answer for every other one.
        new ServiceCollection()
            .AddModuleTransactionManager<RecordingTransactionManager>(typeof(TestCommand).Assembly)
            .ShouldNotContain(service => !service.IsKeyedService);
    }

    [Fact]
    public void AddModuleTransactionManager_WithANullAssembly_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => new ServiceCollection()
                .AddModuleTransactionManager<RecordingTransactionManager>(null!));
    }
}
