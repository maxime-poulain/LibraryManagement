using LibraryManagement.Circulation.Application.Holds.CancelHold;
using LibraryManagement.Circulation.Application.Holds.PlaceHold;
using LibraryManagement.Circulation.Application.Loans.CheckOutCopy;
using LibraryManagement.Circulation.Application.Loans.DeclareLoanLost;
using LibraryManagement.Circulation.Application.Loans.RenewLoan;
using LibraryManagement.Circulation.Application.Loans.ReturnCopy;

namespace LibraryManagement.Host.Http;

/// <summary>
/// The desk: who has what, until when, and who is waiting.
/// </summary>
/// <remarks>
/// <para>
/// Six routes out of sixteen commands, and the ten that are missing are missing for two different
/// reasons. Seven belong to the daily process — reminders, expiries, the reconciliation sweeps —
/// which the design gives to a scheduled run precisely because the passage of time is not an event
/// somebody clicks.
/// </para>
/// <para>
/// The other three are reactions: a hold released because Holdings said the copy left service, a
/// borrower's claims cancelled because Charges said they owe money, a loan's frozen lateness priced
/// because Holdings said the copy turned up. Each is a module answering another module, and a route
/// would let a request assert a fact its announcer never made.
/// </para>
/// </remarks>
internal static class CirculationEndpoints
{
    public static IEndpointRouteBuilder MapCirculation(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGroup("/circulation").WithTags("Circulation")
            .Command<CheckOutCopyCommand>("/loans")
            .Command<RenewLoanCommand>("/loans/renew")
            // The member who says the book is gone, and often that they will pay for it. A door
            // the desk needs, and the same act the scheduled run reaches thirty days later.
            .Command<DeclareLoanLostCommand>("/loans/declare-lost")
            // Damage rides the return, because the librarian with the object in hand is who judges
            // it — which settles whose loan spoiled the copy by construction.
            .Command<ReturnCopyCommand>("/returns")
            .Command<PlaceHoldCommand>("/holds")
            .Command<CancelHoldCommand>("/holds/cancel");

        return routes;
    }
}
