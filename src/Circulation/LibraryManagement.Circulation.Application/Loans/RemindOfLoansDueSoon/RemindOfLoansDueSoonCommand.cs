using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RemindOfLoansDueSoon;

/// <summary>
/// Tells the borrowers whose copies fall due shortly, once each. The courtesy half of the
/// scheduled process.
/// </summary>
/// <remarks>
/// <para>
/// No field, and no validator: the day is the clock's to say, the schedule the policy's, and a
/// command with no shape has nothing for a validator to check — which the shared validator
/// documents as a decision rather than an omission.
/// </para>
/// <para>
/// <strong>A batch, made safe by idempotence.</strong> One command covers every loan the day
/// concerns, in one transaction. Running it twice changes nothing and notifies nobody twice,
/// because each loan records what it has already announced — which is also why a failed run
/// costs nothing but a repeat.
/// </para>
/// </remarks>
public sealed record RemindOfLoansDueSoonCommand : ICommand<Result>;
