using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.Infrastructure.IntegrationEvents;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// The boundary rule of the cross-module passage, applied to every module and exercised against
/// deliberate violations.
/// </summary>
public sealed class IntegrationContractRulesTests
{
    // --- The rule, applied to production code ----------------------------------------------------

    [Fact]
    public void EverySubscriber_ReactsToAPublishedLanguage()
    {
        var violations = IntegrationContractRules
            .FindSubscribersReachingPastAPublishedLanguage(ProductionCode.Assemblies());

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void TheScan_FindsTheSubscribersTheModulesDeclare()
    {
        // Green over nothing reads exactly like green over everything, and this rule is more exposed
        // to it than most: a solution where no module subscribes to anything passes it perfectly.
        var subscribers = IntegrationContractRules
            .FindSubscribers(ProductionCode.Assemblies())
            .Select(subscriber => subscriber.FullName)
            .ToList();

        subscribers.ShouldContain(typeof(DeclareCopyLostOnCopyReportedLost).FullName);
    }

    // --- The rule itself, exercised against deliberate violations --------------------------------

    [Fact]
    public void TheRule_CatchesASubscriberReachingIntoAnotherModulesDomain()
    {
        var violations = IntegrationContractRules
            .FindSubscribersReachingPastAPublishedLanguage(ProductionCode.Fixtures);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(SubscriberOnADomainEvent), StringComparison.Ordinal)
            && violation.Contains(nameof(LoanDeclaredLost), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_LeavesASubscriberOnAPublishedContractAlone()
    {
        var violations = IntegrationContractRules
            .FindSubscribersReachingPastAPublishedLanguage(ProductionCode.Fixtures);

        violations.ShouldNotContain(violation =>
            violation.Contains(nameof(CompliantSubscriber), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_ExplainsWhyRatherThanJustNamingTheType()
    {
        // A failing architecture test is read by whoever broke it. The message is the documentation.
        var violation = IntegrationContractRules
            .FindSubscribersReachingPastAPublishedLanguage(ProductionCode.Fixtures)[0];

        violation.ShouldContain("published contracts");
        violation.ShouldContain("compile-time dependencies");
    }

    [Fact]
    public void TheRule_WithNullAssemblies_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => IntegrationContractRules.FindSubscribersReachingPastAPublishedLanguage(null!));
    }
}

// A deliberate violation and its compliant counterpart. The violation is the mistake that compiles:
// subscribing straight to the announcing module's domain event, which works and quietly makes this
// module depend on that one's model.
public sealed class SubscriberOnADomainEvent : IIntegrationEventSubscriber<LoanDeclaredLost>
{
    public ValueTask HandleAsync(LoanDeclaredLost contract, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}

public sealed class CompliantSubscriber : IIntegrationEventSubscriber<CopyReportedLost>
{
    public ValueTask HandleAsync(CopyReportedLost contract, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
