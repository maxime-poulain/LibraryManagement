using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Migrations.SqlServer;

/// <summary>Builds a <see cref="CirculationDbContext"/> for the migrations tooling.</summary>
public sealed class CirculationDesignTimeFactory : ModuleDesignTimeFactory<CirculationDbContext>
{
    /// <inheritdoc/>
    protected override void Configure(DbContextOptionsBuilder<CirculationDbContext> options)
        => options.UseCirculationSqlServer(DesignTimeOnly);

    /// <inheritdoc/>
    protected override CirculationDbContext Build(DbContextOptions<CirculationDbContext> options) => new(options);
}
