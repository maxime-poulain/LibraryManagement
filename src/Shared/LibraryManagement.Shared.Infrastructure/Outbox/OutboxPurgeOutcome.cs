namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// What one purge run removed from a module's outbox.
/// </summary>
/// <param name="Delivered">
/// Settled rows whose work was done. A number that only says how much history the window holds.
/// </param>
/// <param name="Dead">
/// Rows nobody could deliver, now gone. Never routine: each one is a fact that never reached its
/// reader and never will, and the count is the honest measure of what the window cost.
/// </param>
public sealed record OutboxPurgeOutcome(int Delivered, int Dead);
