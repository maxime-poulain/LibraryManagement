using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Infrastructure.Behaviors;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Extensions;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;
using LibraryManagement.Shared.Infrastructure.UnitOfWork;
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
    public void AddSharedInfrastructure_RegistersTheMessageValidator()
    {
        var descriptor = new ServiceCollection()
            .AddSharedInfrastructure()
            .Single(service => service.ServiceType == typeof(IMessageValidator));

        descriptor.ImplementationType.ShouldBe(typeof(FluentValidationMessageValidator));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersTheTransactionManagerResolver()
    {
        var descriptor = new ServiceCollection()
            .AddSharedInfrastructure()
            .Single(service => service.ServiceType == typeof(IUnitOfWorkResolver));

        descriptor.ImplementationType.ShouldBe(typeof(ModuleUnitOfWorkResolver));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersValidationBeforeTheUnitOfWork()
    {
        // The order is the guarantee. Registered the other way round, everything still compiles and
        // every other test still passes — the only thing that changes is that a command rejected
        // for a missing field starts writing to the store. Nothing else would report it.
        new ServiceCollection()
            .AddSharedInfrastructure()
            .Where(service => service.ServiceType == typeof(Mediator.IPipelineBehavior<,>))
            .Select(service => service.ImplementationType)
            .ShouldBe([typeof(ValidationBehavior<,>), typeof(UnitOfWorkBehavior<,>)]);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersNothingElse()
    {
        // A module owns its own DbContext, repositories, validators and IUnitOfWork. Shared
        // infrastructure that registered anything module-specific would defeat the separation.
        new ServiceCollection().AddSharedInfrastructure().Count.ShouldBe(6);
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersNoTransactionManagerOfItsOwn()
    {
        // The resolver is shared; the managers it finds are not. Shared infrastructure registering
        // one would give every module the same store.
        new ServiceCollection()
            .AddSharedInfrastructure()
            .ShouldNotContain(service => service.ServiceType == typeof(IUnitOfWork));
    }

    [Fact]
    public void AddSharedInfrastructure_WithANullCollection_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddSharedInfrastructure());
    }

    [Fact]
    public void AddModuleUnitOfWork_RegistersItKeyedByTheModule()
    {
        var descriptor = new ServiceCollection()
            .AddModuleUnitOfWork<RecordingUnitOfWork>(typeof(TestCommand).Assembly)
            .Single();

        descriptor.ServiceType.ShouldBe(typeof(IUnitOfWork));
        descriptor.IsKeyedService.ShouldBeTrue();
        descriptor.ServiceKey.ShouldBe(typeof(TestCommand).Assembly);
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddModuleUnitOfWork_RegistersNothingUnkeyed()
    {
        // An unkeyed registration is exactly the defect this whole mechanism exists to prevent: the
        // last module to register would answer for every other one.
        new ServiceCollection()
            .AddModuleUnitOfWork<RecordingUnitOfWork>(typeof(TestCommand).Assembly)
            .ShouldNotContain(service => !service.IsKeyedService);
    }

    [Fact]
    public void AddModuleUnitOfWork_WithANullAssembly_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => new ServiceCollection()
                .AddModuleUnitOfWork<RecordingUnitOfWork>(null!));
    }
}
