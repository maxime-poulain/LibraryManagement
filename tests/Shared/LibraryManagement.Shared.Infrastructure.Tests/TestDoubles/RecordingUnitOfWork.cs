using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

// Hands the behavior one fixed unit of work whatever the command, so tests of the behavior's own
// logic need no container. Resolution by module is covered on its own.
public sealed class FixedUnitOfWorkResolver(IUnitOfWork unitOfWork) : IUnitOfWorkResolver
{
    public ICommandBase? LastCommand { get; private set; }

    public IUnitOfWork Resolve(ICommandBase command)
    {
        LastCommand = command;
        return unitOfWork;
    }
}

// Counts writes. Whether a command's work was written is now a single yes-or-no rather than a
// sequence of transaction calls, which is the point of dropping the explicit transaction.
public sealed class RecordingUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public bool Saved => SaveCount > 0;

    public ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return ValueTask.CompletedTask;
    }
}

// A second implementation, so a test can register two modules and tell which one answered.
public sealed class AnotherModuleUnitOfWork : IUnitOfWork
{
    public ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}

// Fails the way a real store does when someone else got there first.
public sealed class ConflictingUnitOfWork : IUnitOfWork
{
    public ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException();
}
