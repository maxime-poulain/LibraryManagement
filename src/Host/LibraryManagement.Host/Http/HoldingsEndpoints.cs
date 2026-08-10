using LibraryManagement.Holdings.Application.Copies.AcquireCopy;
using LibraryManagement.Holdings.Application.Copies.DeclareCopyLost;
using LibraryManagement.Holdings.Application.Copies.FindCopy;
using LibraryManagement.Holdings.Application.Copies.RecordCopyCondition;
using LibraryManagement.Holdings.Application.Copies.RelabelCopy;
using LibraryManagement.Holdings.Application.Copies.ReleaseCopyForLending;
using LibraryManagement.Holdings.Application.Copies.ReshelveCopy;
using LibraryManagement.Holdings.Application.Copies.RestrictCopyToReference;
using LibraryManagement.Holdings.Application.Copies.ReturnCopyFromRepair;
using LibraryManagement.Holdings.Application.Copies.SendCopyForRepair;
using LibraryManagement.Holdings.Application.Copies.WithdrawCopy;

namespace LibraryManagement.Host.Http;

/// <summary>
/// The stock: what the library owns, where it is, and what state it is in.
/// </summary>
/// <remarks>
/// <c>NoteCopyAccountedFor</c> has no route. It exists because Circulation announces every return
/// and a copy this module holds as lost must heal when one turns up in a borrower's hands — a
/// reaction, not a desk act, and the desk act that resembles it is <c>find</c>, which is here.
/// </remarks>
internal static class HoldingsEndpoints
{
    public static IEndpointRouteBuilder MapHoldings(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGroup("/holdings").WithTags("Holdings")
            .Command<AcquireCopyCommand>("/copies")
            .Command<RelabelCopyCommand>("/copies/relabel")
            .Command<ReshelveCopyCommand>("/copies/reshelve")
            .Command<RecordCopyConditionCommand>("/copies/record-condition")
            .Command<SendCopyForRepairCommand>("/copies/send-for-repair")
            .Command<ReturnCopyFromRepairCommand>("/copies/return-from-repair")
            .Command<RestrictCopyToReferenceCommand>("/copies/restrict-to-reference")
            .Command<ReleaseCopyForLendingCommand>("/copies/release-for-lending")
            // A stocktake that fails to find a copy reaches the same fact Circulation's write-off
            // does, which is why this is a desk act as well as a subscriber's command.
            .Command<DeclareCopyLostCommand>("/copies/declare-lost")
            .Command<FindCopyCommand>("/copies/find")
            .Command<WithdrawCopyCommand>("/copies/withdraw");

        return routes;
    }
}
