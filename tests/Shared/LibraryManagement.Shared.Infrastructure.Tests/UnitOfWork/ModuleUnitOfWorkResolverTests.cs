using System.Reflection;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Extensions;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;
using LibraryManagement.Shared.Infrastructure.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Tests.UnitOfWork;

public sealed class ModuleUnitOfWorkResolverTests
{
    // TestCommand lives in this assembly. Result lives in the shared domain assembly. Two assemblies
    // is all it takes to stand in for two modules, which is the whole point being tested.
    private static readonly Assembly ThisModule = typeof(TestCommand).Assembly;
    private static readonly Assembly AnotherModule = typeof(Result).Assembly;

    private static ModuleUnitOfWorkResolver ResolverOver(IServiceCollection services)
        => new(services.BuildServiceProvider());

    [Fact]
    public void Resolve_ReturnsTheUnitOfWorkRegisteredForTheCommandsModule()
    {
        var resolver = ResolverOver(
            new ServiceCollection()
                .AddModuleUnitOfWork<RecordingUnitOfWork>(ThisModule));

        resolver.Resolve(new TestCommand()).ShouldBeOfType<RecordingUnitOfWork>();
    }

    [Fact]
    public void Resolve_IgnoresAUnitOfWorkRegisteredForAnotherModule()
    {
        // The defect this whole mechanism exists to prevent: without keying, this registration would
        // answer for every command in the process.
        var resolver = ResolverOver(
            new ServiceCollection()
                .AddModuleUnitOfWork<RecordingUnitOfWork>(AnotherModule));

        Should.Throw<InvalidOperationException>(() => resolver.Resolve(new TestCommand()));
    }

    [Fact]
    public void Resolve_WithSeveralModulesRegistered_PicksTheCommandsOwn()
    {
        var resolver = ResolverOver(
            new ServiceCollection()
                .AddModuleUnitOfWork<AnotherModuleUnitOfWork>(AnotherModule)
                .AddModuleUnitOfWork<RecordingUnitOfWork>(ThisModule));

        // Registered last, and still not the one that answers for the other module's commands.
        resolver.Resolve(new TestCommand()).ShouldBeOfType<RecordingUnitOfWork>();
    }

    [Fact]
    public void Resolve_WithNothingRegistered_Throws()
    {
        var resolver = ResolverOver(new ServiceCollection());

        Should.Throw<InvalidOperationException>(() => resolver.Resolve(new TestCommand()));
    }

    [Fact]
    public void Resolve_WithNothingRegistered_NamesTheCommandInTheMessage()
    {
        // A wiring mistake is found by reading the message, so it has to say which command could not
        // be placed and what to do about it.
        var resolver = ResolverOver(new ServiceCollection());

        var thrown = Should.Throw<InvalidOperationException>(() => resolver.Resolve(new TestCommand()));

        thrown.Message.ShouldContain(typeof(TestCommand).FullName!);
        thrown.Message.ShouldContain("AddModuleUnitOfWork");
    }

    [Fact]
    public void Resolve_WithANullCommand_Throws()
    {
        var resolver = ResolverOver(new ServiceCollection());

        Should.Throw<ArgumentNullException>(() => resolver.Resolve(null!));
    }

    [Fact]
    public void Resolve_ReturnsAUnitOfWorkScopedToTheCurrentScope()
    {
        // A unit of work wraps the module's DbContext, which is scoped. Resolving from the root
        // would hand two concurrent commands the same change tracker.
        var provider = new ServiceCollection()
            .AddModuleUnitOfWork<RecordingUnitOfWork>(ThisModule)
            .BuildServiceProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var fromFirst = new ModuleUnitOfWorkResolver(first.ServiceProvider)
            .Resolve(new TestCommand());
        var fromSecond = new ModuleUnitOfWorkResolver(second.ServiceProvider)
            .Resolve(new TestCommand());

        fromFirst.ShouldNotBeSameAs(fromSecond);
    }
}
