using LibraryManagement.Shared.Application;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

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
