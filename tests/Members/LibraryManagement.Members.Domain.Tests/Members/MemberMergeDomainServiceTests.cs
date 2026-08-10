using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using static LibraryManagement.Members.Domain.Tests.Registry;

namespace LibraryManagement.Members.Domain.Tests.Members;

/// <summary>
/// The rules that decide whether two records may be joined into one person.
/// </summary>
/// <remarks>
/// A real instance and not a double: the abstraction exists so the handler can be given a
/// collaborator, not so this can be faked. There is nothing to simulate in logic that reaches no
/// store, and a stub here would assert the stub.
/// </remarks>
public sealed class MemberMergeDomainServiceTests
{
    private readonly MemberMergeDomainService _merge = new();

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public void Merge_PointsTheAbsorbedRecordAtItsSurvivor()
    {
        var absorbed = AMember().Settled();
        var surviving = AMember().Settled();

        _merge.Merge(absorbed, surviving).HasErrors().ShouldBeFalse();

        absorbed.MergedInto.ShouldBe(surviving.Id);
    }

    [Fact]
    public void Merge_RaisesTheEventTheDownstreamContextsWaitOn()
    {
        var absorbed = AMember().Settled();
        var surviving = AMember().Settled();

        _merge.Merge(absorbed, surviving);

        var merged = absorbed.Event<MembersMerged>();
        merged.AbsorbedMemberId.ShouldBe(absorbed.Id);
        merged.SurvivingMemberId.ShouldBe(surviving.Id);
    }

    [Fact]
    public void Merge_LeavesTheSurvivorUntouched()
    {
        // Two records are read; one is written. That is what keeps a merge inside the unit of
        // consistency the command already was.
        var absorbed = AMember().Settled();
        var surviving = AMember().Settled();

        _merge.Merge(absorbed, surviving);

        surviving.MergedInto.ShouldBeNull();
        surviving.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_ARecordIntoItself_IsRefused()
    {
        var member = AMember().Settled();

        // Alone, and not alongside the others: every rule below reads as nonsense about one record
        // compared with itself, and reporting them would describe a mistake nobody made.
        CodesOf(_merge.Merge(member, member))
            .ShouldHaveSingleItem()
            .ShouldBe(MembersErrorCodes.MemberCannotAbsorbItself);

        member.MergedInto.ShouldBeNull();
    }

    [Fact]
    public void Merge_ARecordAlreadyMerged_IsRefused()
    {
        // Neither silence nor a chain: a record merged twice would leave whoever holds its
        // identifier pointing at a pointer, and a consumer follows it once.
        var absorbed = AMember().Settled();
        _merge.Merge(absorbed, AMember());
        var surviving = AMember().Settled();

        CodesOf(_merge.Merge(absorbed, surviving))
            .ShouldContain(MembersErrorCodes.MemberAlreadyMerged);

        absorbed.MergedInto.ShouldNotBe(surviving.Id);
    }

    [Fact]
    public void Merge_IntoARecordThatWasItselfMerged_IsRefused()
    {
        var absorbed = AMember().Settled();
        var surviving = AMember().Settled();
        _merge.Merge(surviving, AMember());

        CodesOf(_merge.Merge(absorbed, surviving))
            .ShouldContain(MembersErrorCodes.MemberAlreadyMerged);

        absorbed.MergedInto.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Merge_WithAnErasedRecordOnEitherSide_IsRefused(bool absorbedIsErased)
    {
        // An erased record has no name, no card and no address left, so nothing remains by which
        // anyone could have judged it a duplicate. A merge is a judgement about people.
        var absorbed = AMember().Settled();
        var surviving = AMember().Settled();
        (absorbedIsErased ? absorbed : surviving).Erase(Today);

        CodesOf(_merge.Merge(absorbed, surviving)).ShouldContain(MembersErrorCodes.MemberErased);

        absorbed.MergedInto.ShouldBeNull();
    }

    [Fact]
    public void Merge_ReportsEveryRefusalAndNotOnlyTheFirst()
    {
        // Somebody looking at two records, one already merged away and the other erased, is better
        // served learning both than making two trips.
        var absorbed = AMember().Settled();
        _merge.Merge(absorbed, AMember());
        var surviving = AMember().Settled();
        surviving.Erase(Today);

        var codes = CodesOf(_merge.Merge(absorbed, surviving));

        codes.ShouldContain(MembersErrorCodes.MemberAlreadyMerged);
        codes.ShouldContain(MembersErrorCodes.MemberErased);
    }

    [Fact]
    public void Merge_NamesWhichSideEachRefusalConcerns()
    {
        // The message is what the member of staff reads, and "one of these two was already merged"
        // sends them back to look at both.
        var absorbed = AMember().Settled();
        _merge.Merge(absorbed, AMember());

        var messages = _merge.Merge(absorbed, AMember())
            .Match(() => [], errors => errors.Select(error => error.ErrorMessage).ToList());

        messages.ShouldContain(message => message.Contains("absorbed", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_DemandsBothRecords()
    {
        Should.Throw<ArgumentNullException>(() => _merge.Merge(null!, AMember()));
        Should.Throw<ArgumentNullException>(() => _merge.Merge(AMember(), null!));
    }
}
