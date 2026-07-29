namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// What one drain run did, and what stopped it if something did.
/// </summary>
/// <param name="Delivered">How many messages were delivered.</param>
/// <param name="Blockage">
/// The failure that ended the run, or <see langword="null"/> when the run ended because the outbox
/// was drained or the batch limit was reached.
/// </param>
/// <remarks>
/// The processor reports; the trigger decides. Under the current host the recurring drain is also
/// the retry, and the report is observability — the outcome rides along with the job, so a blockage
/// is visible without querying the table. A host that ever needs a nearer retry — a heartbeat far
/// slower than a minute, a handler whose latency the business feels — reads the blockage and
/// schedules one, without the processor ever learning that a scheduler exists.
/// </remarks>
public sealed record OutboxDrainOutcome(int Delivered, OutboxBlockage? Blockage);

/// <summary>
/// The message a drain run stopped on.
/// </summary>
/// <param name="MessageId">The row that failed. Always the head of the queue — order is the contract.</param>
/// <param name="Attempts">How many deliveries have now failed, this one included.</param>
/// <param name="Dead">
/// Whether this failure was the last allowed one. A dead head no longer blocks: the next run skips
/// it, so a prompt follow-up drains the queue waiting behind the corpse.
/// </param>
public sealed record OutboxBlockage(long MessageId, int Attempts, bool Dead);
