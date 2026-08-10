using LibraryManagement.Circulation.Application;
using LibraryManagement.Circulation.Application.Borrowers.GetBorrowerFile;
using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Infrastructure.Queries;

/// <summary>
/// Handles <see cref="GetBorrowerFileQuery"/> against the module's store, and asks Charges for the
/// one figure the verdict is made from.
/// </summary>
/// <param name="context">The module's store.</param>
/// <param name="balances">Charges' answer, through the port this module declared.</param>
/// <param name="policy">The policy holding what a debt forbids.</param>
/// <remarks>
/// <para>
/// The only query handler here that reaches outside its own store, and it reaches through the port
/// this context already declared for its desk paths — not to another module's query, which
/// <c>strategic-design.md</c> §10 forbids: a module that answered a page by calling another
/// module's query would put the composition back inside the modules it was lifted out of.
/// </para>
/// <para>
/// <strong>The balance is read twice while this page is built</strong> — once here, to judge the
/// standing, and once by Charges' own query, to display the figure. That is deliberate. The
/// alternative is for the composer to derive the verdict from the amount it already holds, and
/// §10 refuses it in terms: a rule that slips into a composer is a rule no module's invariants
/// cover. Two reads of a handful of rows is the price of the judgement staying where the glossary
/// puts it, and it is small — a charge that ends leaves the account, so what one person owes is
/// never many rows.
/// </para>
/// </remarks>
public sealed class GetBorrowerFileQueryHandler(
    CirculationDbContext context,
    IMemberBalance balances,
    CirculationPolicy policy)
    : IQueryHandler<GetBorrowerFileQuery, BorrowerFileDto>
{
    /// <inheritdoc/>
    public async ValueTask<Result<BorrowerFileDto>> Handle(
        GetBorrowerFileQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var borrowerId = BorrowerId.Create(query.BorrowerId);

        // What the borrower still has to answer for. A returned loan is history, and this file is
        // the present tense. The status is turned into its name in memory rather than in the
        // query: asking the store for it would have the column cast on the way out, to no end,
        // since it is already stored as its name.
        var loans = await context.Loans
            .AsNoTracking()
            .Where(loan => loan.BorrowerId == borrowerId && loan.Status != LoanStatus.Returned)
            .OrderByDescending(loan => loan.CheckedOutOn)
            .Select(loan => new
            {
                loan.Id,
                loan.CopyId,
                loan.EditionId,
                loan.CheckedOutOn,
                loan.DueDate,
                loan.RenewalCount,
                loan.Status,
                loan.DeclaredLostOn,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A borrower's claims are spread over as many queues as there are editions, so this reads
        // across the aggregates rather than through one — legitimate here, and only here, because
        // nothing read is ever written back. The store joins the queue to its holds and filters on
        // the borrower index the cap count already needed.
        //
        // The queue and the hold are paired before either is filtered on purpose: filtering inside
        // the collection selector reads better and does not translate, which is a thing this shape
        // cannot be discovered to be until it runs.
        var holds = await context.HoldQueues
            .AsNoTracking()
            .SelectMany(queue => queue.Holds, (queue, hold) => new { EditionId = queue.Id, Hold = hold })
            .Where(claim => claim.Hold.BorrowerId == borrowerId)
            .OrderBy(claim => claim.Hold.PlacedOn)
            .Select(claim => new
            {
                claim.Hold.Id,
                claim.EditionId,
                claim.Hold.Status,
                claim.Hold.PlacedOn,
                claim.Hold.TrappedCopyId,
                claim.Hold.PickupDeadline,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var blocked = await Standing
            .IsBlockedAsync(borrowerId, balances, policy, cancellationToken)
            .ConfigureAwait(false);

        return Result<BorrowerFileDto>.Success(
            new BorrowerFileDto(
                query.BorrowerId,
                loans
                    .Select(loan => new LoanOnFileDto(
                        loan.Id.Value,
                        loan.CopyId.Value,
                        loan.EditionId.Value,
                        loan.CheckedOutOn,
                        loan.DueDate,
                        loan.RenewalCount,
                        loan.Status.ToString(),
                        loan.DeclaredLostOn))
                    .ToArray(),
                holds
                    .Select(hold => new HoldOnFileDto(
                        hold.Id.Value,
                        hold.EditionId.Value,
                        hold.Status.ToString(),
                        hold.PlacedOn,
                        hold.TrappedCopyId?.Value,
                        hold.PickupDeadline))
                    .ToArray(),
                blocked));
    }
}
