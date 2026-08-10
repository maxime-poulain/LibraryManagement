using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Migrations.SqlServer;

/// <summary>
/// Points a module's store at SQL Server, at that module's migrations, and at a history table of
/// its own.
/// </summary>
/// <remarks>
/// <para>
/// The one place the provider call is spelled. Every module has a named wrapper around this — the
/// host says <c>UseCatalogSqlServer</c>, not this — and every one of those wrappers is a single
/// expression, because what they would otherwise repeat is the part that fails silently when it is
/// forgotten.
/// </para>
/// <para>
/// <strong>The history table lives in the module's own schema.</strong> Five contexts share one
/// database; left at the default they would share <c>dbo.__EFMigrationsHistory</c>, and each would
/// then read the others' rows as migrations of its own that it has never applied. The failure looks
/// like corruption rather than like a configuration mistake, which is why the setting is not left
/// to a caller to remember.
/// </para>
/// <para>
/// This lives beside the host and not in a module, for the reason <c>migrations.md</c> §2 gives:
/// a migration is provider-specific down to its generated code, and a module has a written rule
/// against naming an engine.
/// </para>
/// </remarks>
public static class ModuleSqlServer
{
    private const string HistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Configures SQL Server for a module's context.
    /// </summary>
    /// <param name="options">The builder to configure.</param>
    /// <param name="connectionString">The database the host chose.</param>
    /// <param name="schema">The module's schema, which its history table shares.</param>
    /// <param name="migrations">The assembly holding that module's migrations.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="migrations"/> is null.</exception>
    public static DbContextOptionsBuilder UseModuleSqlServer(
        this DbContextOptionsBuilder options,
        string connectionString,
        string schema,
        Assembly migrations)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(migrations);

        return options.UseSqlServer(connectionString, sql => sql
            .MigrationsAssembly(migrations.GetName().Name)
            .MigrationsHistoryTable(HistoryTable, schema));
    }

    /// <summary>
    /// Configures SQL Server for a typed builder, which is what a caller building its own context
    /// holds.
    /// </summary>
    /// <typeparam name="TContext">The context being configured.</typeparam>
    /// <param name="options">The builder to configure.</param>
    /// <param name="connectionString">The database the caller chose.</param>
    /// <param name="schema">The module's schema, which its history table shares.</param>
    /// <param name="migrations">The assembly holding that module's migrations.</param>
    /// <returns>The same builder, still typed, so <c>Options</c> fits the context's constructor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <remarks>
    /// The pair EF Core's own provider extensions come in. A host configures through the untyped
    /// builder <c>AddDbContext</c> hands it; a test fixture and the design-time factory build their
    /// own typed one, and untyped options do not fit a context's constructor.
    /// </remarks>
    public static DbContextOptionsBuilder<TContext> UseModuleSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> options,
        string connectionString,
        string schema,
        Assembly migrations)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);

        UseModuleSqlServer((DbContextOptionsBuilder)options, connectionString, schema, migrations);

        return options;
    }
}
