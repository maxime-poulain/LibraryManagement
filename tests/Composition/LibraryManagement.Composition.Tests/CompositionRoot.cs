using LibraryManagement.Shared.Infrastructure.Behaviors;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests;

/// <summary>
/// The one <c>AddMediator</c> call in this assembly — and therefore the one place its pipeline
/// is declared.
/// </summary>
/// <remarks>
/// <para>
/// <c>options.PipelineBehaviors</c> is the library's own way to declare the pipeline, and the
/// source generator parses this very syntax at compile time: the behaviors must be inline
/// <c>typeof</c> expressions, and there must be exactly one such call per assembly — a second one
/// carrying a different array would leave the generator to pick a winner. From the array it emits
/// one closed registration per message and behavior, honoring each behavior's constraints, so
/// the unit of work is never even constructed for a query.
/// </para>
/// <para>
/// <strong>The order of the array is the guarantee.</strong> Logging first is outermost: what
/// validation refuses is still a line in the log, and the duration covers the whole pipeline.
/// Validation before the unit of work is what keeps a command rejected for a missing field from
/// ever reaching the store. The composition tests pin both, from the outside.
/// </para>
/// <para>
/// Scoped, not the default singleton: the outbox drain opens a scope per message, and a singleton
/// mediator would resolve every handler from the root scope — the handler would write to a
/// context nobody saves. <c>OutboxTests</c> proves it where it bites.
/// </para>
/// </remarks>
internal static class CompositionRoot
{
    /// <summary>
    /// A service collection carrying the mediator, its pipeline and the shared infrastructure —
    /// everything a composition adds its modules to.
    /// </summary>
    public static IServiceCollection Services() => new ServiceCollection()
        .AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(UnitOfWorkBehavior<,>),
            ];
        })
        .AddSharedInfrastructure();
}
