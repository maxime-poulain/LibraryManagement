using LibraryManagement.Catalog.Application.Works.GetWorkById;
using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// The naming rule, applied to every module and exercised against deliberate violations.
/// </summary>
public sealed class QueryContractRulesTests
{
    // --- The rule, applied to production code ----------------------------------------------------

    [Fact]
    public void EveryQuery_AnswersWithADto()
    {
        var violations = QueryContractRules.FindQueriesNotAnsweringWithADto(ProductionCode.Assemblies());

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void TheScan_FindsTheQueriesTheModulesDeclare()
    {
        // A naming rule is more exposed than most to passing over nothing, since a solution with no
        // query at all looks exactly like a compliant one. Compared by name rather than by type, for
        // the reason ProductionCode gives.
        var queries = QueryContractRules
            .FindQueries(ProductionCode.Assemblies())
            .Select(query => query.FullName)
            .ToList();

        queries.ShouldContain(typeof(GetWorkByIdQuery).FullName);
    }

    // --- The rule itself, exercised against deliberate violations --------------------------------

    [Fact]
    public void TheRule_CatchesAQueryAnsweringWithAnUnsuffixedType()
    {
        var violations = QueryContractRules.FindQueriesNotAnsweringWithADto(ProductionCode.Fixtures);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(BareAnswerQuery), StringComparison.Ordinal)
            && violation.Contains(nameof(WorkSummary), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_LooksThroughACollectionAtTheRow()
    {
        // A query answering with many rows is judged on the row. Without the unwrapping it would be
        // judged on IReadOnlyList, which no convention could ever apply to.
        var violations = QueryContractRules.FindQueriesNotAnsweringWithADto(ProductionCode.Fixtures);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(ManyBareAnswersQuery), StringComparison.Ordinal)
            && violation.Contains(nameof(WorkSummary), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_LeavesACompliantQueryAlone()
    {
        var violations = QueryContractRules.FindQueriesNotAnsweringWithADto(ProductionCode.Fixtures);

        violations.ShouldNotContain(violation =>
            violation.Contains(nameof(CompliantQuery), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_LeavesACompliantCollectionAlone()
    {
        var violations = QueryContractRules.FindQueriesNotAnsweringWithADto(ProductionCode.Fixtures);

        violations.ShouldNotContain(violation =>
            violation.Contains(nameof(CompliantCollectionQuery), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_LeavesATypeFromOutsideTheSolutionAlone()
    {
        // A count has no name to suffix, and demanding a wrapper for one would be ceremony rather
        // than a boundary.
        var violations = QueryContractRules.FindQueriesNotAnsweringWithADto(ProductionCode.Fixtures);

        violations.ShouldNotContain(violation =>
            violation.Contains(nameof(WorkCountQuery), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_ExplainsWhyRatherThanJustNamingTheType()
    {
        // A failing architecture test is read by whoever broke it. The message is the documentation.
        var violation = QueryContractRules.FindQueriesNotAnsweringWithADto(ProductionCode.Fixtures)[0];

        violation.ShouldContain("handed out");
        violation.ShouldContain("export the module's model");
    }

    [Fact]
    public void TheRule_FindsTheQueriesItIsSupposedToInspect()
    {
        var queries = QueryContractRules.FindQueries(ProductionCode.Fixtures);

        queries.ShouldContain(typeof(CompliantQuery));
        queries.ShouldContain(typeof(BareAnswerQuery));
    }

    [Fact]
    public void TheRule_WithNullAssemblies_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => QueryContractRules.FindQueriesNotAnsweringWithADto(null!));
    }
}

// Deliberate violations, and their compliant counterparts. No handler accompanies them: a query is
// declared by the contract it implements, which is all the rule reads.
public sealed record WorkSummary(Guid WorkId, string Title);

public sealed record CatalogEntryDto(Guid WorkId, string Title);

public sealed record BareAnswerQuery : IQuery<WorkSummary>;

public sealed record ManyBareAnswersQuery : IQuery<IReadOnlyList<WorkSummary>>;

public sealed record CompliantQuery : IQuery<CatalogEntryDto>;

public sealed record CompliantCollectionQuery : IQuery<IReadOnlyList<CatalogEntryDto>>;

public sealed record WorkCountQuery : IQuery<int>;
