using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Members.Migrations.SqlServer;

/// <summary>Builds a <see cref="MembersDbContext"/> for the migrations tooling.</summary>
public sealed class MembersDesignTimeFactory : ModuleDesignTimeFactory<MembersDbContext>
{
    /// <inheritdoc/>
    protected override void Configure(DbContextOptionsBuilder<MembersDbContext> options)
        => options.UseMembersSqlServer(DesignTimeOnly);

    /// <inheritdoc/>
    protected override MembersDbContext Build(DbContextOptions<MembersDbContext> options) => new(options);
}
