using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Charges.Migrations.SqlServer;

/// <summary>
/// Points the Charges module's store at SQL Server, at this assembly's migrations, and at a history
/// table in the module's own schema.
/// </summary>
/// <remarks>
/// The name is the module's, the rule is <see cref="ModuleSqlServer"/>'s. Callers say this one —
/// the host at startup, the fixtures before every run, the design-time factory for the tooling —
/// and what they are saying is spelled once, where forgetting it is impossible.
/// </remarks>
public static class ChargesSqlServer
{
    /// <summary>Configures SQL Server for <see cref="ChargesDbContext"/>.</summary>
    /// <param name="options">The builder to configure.</param>
    /// <param name="connectionString">The database the host chose.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static DbContextOptionsBuilder UseChargesSqlServer(
        this DbContextOptionsBuilder options,
        string connectionString)
        => options.UseModuleSqlServer(
            connectionString, ChargesDbContext.Schema, typeof(ChargesSqlServer).Assembly);

    /// <summary>Configures SQL Server for a typed builder.</summary>
    /// <typeparam name="TContext">The context being configured.</typeparam>
    /// <param name="options">The builder to configure.</param>
    /// <param name="connectionString">The database the caller chose.</param>
    /// <returns>The same builder, still typed.</returns>
    public static DbContextOptionsBuilder<TContext> UseChargesSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> options,
        string connectionString)
        where TContext : DbContext
        => options.UseModuleSqlServer(
            connectionString, ChargesDbContext.Schema, typeof(ChargesSqlServer).Assembly);
}
