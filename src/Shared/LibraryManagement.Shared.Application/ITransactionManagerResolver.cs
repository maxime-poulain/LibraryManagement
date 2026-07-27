using System.Reflection;
using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Shared.Application;

/// <summary>
/// Finds the <see cref="ITransactionManager"/> belonging to the module that owns a command.
/// </summary>
/// <remarks>
/// <para>
/// In a modular monolith each module owns its own persistence store and therefore its own
/// <see cref="ITransactionManager"/>. There is one command dispatcher for all of them, so it cannot
/// take a transaction manager by type: several modules register an implementation of the same
/// interface, and the last registration wins. A command dispatched from one module would open a
/// transaction on another module's store, silently, and nothing would fail.
/// </para>
/// <para>
/// A command belongs to exactly one module — the rule that a command never dispatches another
/// command guarantees it — so there is always exactly one right answer. This abstraction is how the
/// dispatcher asks for it without naming a container.
/// </para>
/// </remarks>
public interface ITransactionManagerResolver
{
    /// <summary>
    /// Returns the transaction manager of the module that owns <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command about to be dispatched.</param>
    /// <returns>The transaction manager to open the command's transaction with.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no transaction manager is registered for the command's module. That is a wiring
    /// mistake rather than an expected failure, so it throws instead of returning a
    /// <see cref="Domain.Results.Result"/>: no employee can act on it, and no retry will fix it.
    /// </exception>
    ITransactionManager Resolve(ICommandBase command);
}

/// <summary>
/// Identifies the module a command belongs to.
/// </summary>
/// <remarks>
/// The assembly declaring a command is its module. Nothing else is needed: a module's commands live
/// in its own application assembly, which makes the mapping a fact of the build rather than
/// something every command has to remember to declare through an attribute.
/// </remarks>
public static class ModuleKey
{
    /// <summary>
    /// Returns the key identifying the module that owns <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command to locate.</param>
    /// <returns>The key under which that module's services are registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
    public static object For(ICommandBase command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.GetType().Assembly;
    }

    /// <summary>
    /// Returns the key identifying the module whose commands live in <paramref name="commandAssembly"/>.
    /// </summary>
    /// <param name="commandAssembly">The assembly declaring the module's commands.</param>
    /// <returns>The key to register that module's services under.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="commandAssembly"/> is null.</exception>
    public static object For(Assembly commandAssembly)
    {
        ArgumentNullException.ThrowIfNull(commandAssembly);

        return commandAssembly;
    }
}
