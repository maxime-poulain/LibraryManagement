using System.Text.Json.Serialization;
using LibraryManagement.Members.Application.Members.RenewMembership;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.Extensions;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Members.Infrastructure.Serialization;
using LibraryManagement.Members.PublishedLanguage;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Members.Infrastructure.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    private static IServiceCollection Registered()
        => new ServiceCollection().AddMembersModule(options => options.UseSqlServer("Server=unused"));

    [Fact]
    public void AddMembersModule_RegistersTheModulesOwnStore()
    {
        Registered().ShouldContain(service => service.ServiceType == typeof(MembersDbContext));
    }

    [Theory]
    [InlineData(typeof(IMemberRepository), typeof(MemberRepository))]
    [InlineData(typeof(IMemberEntitlement), typeof(Infrastructure.PublishedLanguage.MemberEntitlement))]
    public void AddMembersModule_RegistersTheDomainAndPublishedPorts(Type port, Type adapter)
    {
        var descriptor = Registered().Single(service => service.ServiceType == port);

        descriptor.ImplementationType.ShouldBe(adapter);
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddMembersModule_RegistersItsUnitOfWorkKeyedByItsOwnCommands()
    {
        // The key is what keeps the modules' units of work apart — this is the third registration
        // of the one interface, and registered unkeyed, the last module added would write every
        // other module's changes through its own store.
        var descriptor = Registered()
            .Single(service => service.ServiceType == typeof(IUnitOfWork));

        descriptor.IsKeyedService.ShouldBeTrue();
        descriptor.ServiceKey.ShouldBe(typeof(RenewMembershipCommand).Assembly);
        descriptor.KeyedImplementationType.ShouldBe(typeof(MembersUnitOfWork));
    }

    [Fact]
    public void AddMembersModule_ContributesEveryValueObjectsJsonConverter()
    {
        // The outbox serializer collects these; one forgotten here is an event that fails to
        // serialize on the first save that raises it.
        var converters = Registered()
            .Where(service => service.ServiceType == typeof(JsonConverter))
            .Select(service => service.ImplementationType)
            .ToList();

        converters.ShouldContain(typeof(CardNumberJsonConverter));
        converters.ShouldContain(typeof(MemberNameJsonConverter));
        converters.ShouldContain(typeof(ContactDetailsJsonConverter));
        converters.ShouldContain(typeof(GuardianJsonConverter));
    }

    [Fact]
    public void AMembersCommand_ResolvesTheMembersUnitOfWork()
    {
        // End to end through the container, which is where the mechanism either works or does not.
        using var provider = new ServiceCollection()
            .AddSharedInfrastructure()
            .AddMembersModule(options => options.UseSqlServer("Server=unused"))
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IUnitOfWorkResolver>();

        resolver.Resolve(new RenewMembershipCommand(Guid.NewGuid()))
            .ShouldBeOfType<MembersUnitOfWork>();
    }
}
