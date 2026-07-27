using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

// Hands the dispatcher one fixed transaction manager whatever the command, so tests of the
// dispatcher's own behaviour need no container. Resolution by module is covered on its own.
public sealed class FixedTransactionManagerResolver(ITransactionManager transactionManager)
    : ITransactionManagerResolver
{
    public ICommandBase? LastCommand { get; private set; }

    public ITransactionManager Resolve(ICommandBase command)
    {
        LastCommand = command;
        return transactionManager;
    }
}

// Records what the dispatcher did with the transaction, in order.
public sealed class RecordingTransactionManager : ITransactionManager
{
    public int BeginCount { get; private set; }

    public RecordingTransaction? Transaction { get; private set; }

    public ValueTask<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        BeginCount++;
        Transaction = new RecordingTransaction();
        return ValueTask.FromResult<ITransaction>(Transaction);
    }
}

// A second implementation, so a test can register two modules and tell which one answered.
public sealed class AnotherModuleTransactionManager : ITransactionManager
{
    public ValueTask<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<ITransaction>(new RecordingTransaction());
}

public sealed class RecordingTransaction : ITransaction
{
    private readonly List<string> _calls = [];

    // Ordered log, so a test can assert that the commit happened before the disposal rather than
    // merely that both happened.
    public IReadOnlyList<string> Calls => _calls;

    public bool Committed => _calls.Contains("commit");

    public bool Disposed => _calls.Contains("dispose");

    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        _calls.Add("commit");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _calls.Add("dispose");
        return ValueTask.CompletedTask;
    }
}
