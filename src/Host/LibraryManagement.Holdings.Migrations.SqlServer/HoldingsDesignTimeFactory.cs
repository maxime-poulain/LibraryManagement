using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Holdings.Migrations.SqlServer;

/// <summary>Builds a <see cref="HoldingsDbContext"/> for the migrations tooling.</summary>
public sealed class HoldingsDesignTimeFactory : ModuleDesignTimeFactory<HoldingsDbContext>
{
    /// <inheritdoc/>
    protected override void Configure(DbContextOptionsBuilder<HoldingsDbContext> options)
        => options.UseHoldingsSqlServer(DesignTimeOnly);

    /// <inheritdoc/>
    protected override HoldingsDbContext Build(DbContextOptions<HoldingsDbContext> options) => new(options);
}
