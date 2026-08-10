using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// The real host, booted against the suite's database.
/// </summary>
/// <param name="connectionString">The database the host is pointed at.</param>
/// <param name="runsJobs">
/// Whether this instance also runs the Hangfire server. Off by default: a background server ticking
/// through a test would drain outboxes underneath assertions about them, and every test that wants
/// the clockwork says so.
/// </param>
/// <param name="clock">
/// A clock to stand in for the system one, for tests that ask questions about days. Registered
/// after the modules, so it wins the resolution the store registration's <c>TryAdd</c> would
/// otherwise have settled.
/// </param>
/// <remarks>
/// <strong>Booting this is itself a test.</strong> Building the host applies all five modules'
/// migrations, constructs Hangfire's storage over the same database, and registers the recurring
/// jobs — so a broken composition, a migration that will not apply, or a schema Hangfire installs
/// in the wrong order fails here, before any assertion runs.
/// </remarks>
internal sealed class TheHost(string connectionString, bool runsJobs = false, TimeProvider? clock = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:LibraryManagement", connectionString);
        builder.UseSetting("Hangfire:RunServer", runsJobs.ToString());

        if (clock is not null)
        {
            builder.ConfigureTestServices(services => services.AddSingleton(clock));
        }
    }

    /// <summary>Runs work in a scope of the host's own container.</summary>
    public async Task InScopeAsync(Func<IServiceProvider, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var scope = Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }

    /// <summary>Runs work in a scope of the host's own container and returns what it produced.</summary>
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var scope = Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }
}

/// <summary>A clock that does not move, for the tests that ask questions about days.</summary>
internal sealed class FrozenClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
