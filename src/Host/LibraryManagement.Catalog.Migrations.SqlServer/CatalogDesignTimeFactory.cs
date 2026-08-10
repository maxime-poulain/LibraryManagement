using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Migrations.SqlServer;

/// <summary>Builds a <see cref="CatalogDbContext"/> for the migrations tooling.</summary>
public sealed class CatalogDesignTimeFactory : ModuleDesignTimeFactory<CatalogDbContext>
{
    /// <inheritdoc/>
    protected override void Configure(DbContextOptionsBuilder<CatalogDbContext> options)
        => options.UseCatalogSqlServer(DesignTimeOnly);

    /// <inheritdoc/>
    protected override CatalogDbContext Build(DbContextOptions<CatalogDbContext> options) => new(options);
}
