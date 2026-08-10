using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Charges.Migrations.SqlServer;

/// <summary>Builds a <see cref="ChargesDbContext"/> for the migrations tooling.</summary>
public sealed class ChargesDesignTimeFactory : ModuleDesignTimeFactory<ChargesDbContext>
{
    /// <inheritdoc/>
    protected override void Configure(DbContextOptionsBuilder<ChargesDbContext> options)
        => options.UseChargesSqlServer(DesignTimeOnly);

    /// <inheritdoc/>
    protected override ChargesDbContext Build(DbContextOptions<ChargesDbContext> options) => new(options);
}
