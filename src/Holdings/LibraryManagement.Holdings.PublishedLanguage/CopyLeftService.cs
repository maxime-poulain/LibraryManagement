namespace LibraryManagement.Holdings.PublishedLanguage;

/// <summary>
/// A copy left the lendable service — sent for repair, restricted to consultation, withdrawn or
/// declared lost. Which of the four is not said, on purpose.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery.
/// </param>
/// <param name="CopyId">The copy that is no longer there to be taken.</param>
/// <remarks>
/// <para>
/// The fact names no status: what the departure means — a promise on the hold shelf that can no
/// longer be kept, most consequentially — is each reader's own rule, and a contract that said
/// <em>why</em> the door closed would invite a reader to grow a use for the reason. The copy and
/// nothing else, exactly as <c>CopyReportedLost</c> crosses the other way.
/// </para>
/// <para>
/// Announced for every departure, including ones nobody downstream reacts to. A fact not
/// published when it happened cannot be recovered afterwards, and the publisher does not know
/// who is listening — that is what makes it an announcement.
/// </para>
/// </remarks>
public sealed record CopyLeftService(Guid EventId, Guid CopyId);

/// <summary>
/// A copy nobody could account for has turned up, and is back in the collection's hands.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery.
/// </param>
/// <param name="CopyId">The copy that turned up.</param>
/// <remarks>
/// One contract, more than one audience, because every reader needs exactly the same thing — the
/// copy: the context that priced its replacement cancels what is still outstanding, and the
/// context that gave up a loan on it settles what the lateness was worth. Neither learns what the
/// other does with it, which is the point of announcing rather than telling.
/// </remarks>
public sealed record CopyRecovered(Guid EventId, Guid CopyId);
