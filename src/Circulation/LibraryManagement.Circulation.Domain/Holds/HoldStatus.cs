namespace LibraryManagement.Circulation.Domain.Holds;

/// <summary>
/// Where a live hold stands. Only live holds have a status: fulfilled, expired and cancelled
/// holds leave the aggregate, their outcome travelling in the event.
/// </summary>
public enum HoldStatus
{
    /// <summary>Waiting for a copy to come back.</summary>
    Queued,

    /// <summary>A returned copy is set aside on the hold shelf, and the pickup countdown runs.</summary>
    AwaitingPickup,
}
