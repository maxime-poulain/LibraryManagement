using LibraryManagement.Charges.Application.Accounts.TakePayment;
using LibraryManagement.Charges.Application.Accounts.WaiveCharge;

namespace LibraryManagement.Host.Http;

/// <summary>
/// The counter: what a member owes, and the two things a member of staff does about it.
/// </summary>
/// <remarks>
/// <strong>Nothing here raises a charge.</strong> A fine, a replacement and a damage charge are all
/// priced from a fact Circulation announced, and each is idempotent by the loan it belongs to. A
/// route that raised one by hand would create money owed with no loan behind it, and would do it
/// outside the memory that keeps a redelivery from charging twice. What the desk does is take money
/// and forgive it; what the library charges is decided by what happened.
/// </remarks>
internal static class ChargesEndpoints
{
    public static IEndpointRouteBuilder MapCharges(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGroup("/charges").WithTags("Charges")
            .Command<TakePaymentCommand>("/payments")
            .Command<WaiveChargeCommand>("/waivers");

        return routes;
    }
}
