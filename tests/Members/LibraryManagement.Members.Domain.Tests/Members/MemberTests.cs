using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using static LibraryManagement.Members.Domain.Tests.Registry;

namespace LibraryManagement.Members.Domain.Tests.Members;

public sealed class MemberTests
{
    private static List<ErrorCode> CodesOf<T>(Result<T> outcome)
        => outcome.Match(_ => [], errors => errors.Select(error => error.ErrorCode).ToList());

    // --- Enrollment --------------------------------------------------------------------------------

    [Fact]
    public void Enroll_StartsTheFirstMembershipToday()
    {
        var member = AMember();

        member.MembershipStart.ShouldBe(Today);
        member.MembershipEnd.ShouldBe(Today.AddMonths(Member.MembershipDurationInMonths));
    }

    [Fact]
    public void Enroll_SaysSo()
    {
        var member = AMember(MemberCategory.Student);

        var enrolled = member.Event<MemberEnrolled>();
        enrolled.MemberId.ShouldBe(member.Id);
        enrolled.Category.ShouldBe(MemberCategory.Student);
        enrolled.CardNumber.ShouldBe(member.CardNumber);
    }

    [Fact]
    public void Enroll_AChildWithoutAGuardian_IsRefused()
    {
        var outcome = Member.Enroll(
            MemberId.Generate(), AName(), new DateOnly(2018, 9, 1), MemberCategory.Child,
            ACardNumber(), AContact(), guardian: null, Today);

        CodesOf(outcome).ShouldContain(MembersErrorCodes.GuardianRequired);
    }

    [Fact]
    public void Enroll_AChildWithAGuardian_Succeeds()
    {
        var outcome = Member.Enroll(
            MemberId.Generate(), AName(), new DateOnly(2018, 9, 1), MemberCategory.Child,
            ACardNumber(), AContact(), AGuardian(), Today);

        outcome.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public void Enroll_AnAdultWithAGuardian_IsOrdinary()
    {
        // A protected adult under tutelle has a guardian too: the child invariant is a floor, not
        // a ceiling.
        AMember(MemberCategory.Adult, AGuardian()).Guardian.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Enroll_ABirthDateNotInThePast_IsRefused(int daysFromToday)
    {
        var outcome = Member.Enroll(
            MemberId.Generate(), AName(), Today.AddDays(daysFromToday), MemberCategory.Adult,
            ACardNumber(), AContact(), guardian: null, Today);

        CodesOf(outcome).ShouldContain(MembersErrorCodes.DateOfBirthNotInThePast);
    }

    [Fact]
    public void Enroll_ABirthDateYesterday_Succeeds()
        => Member.Enroll(
                MemberId.Generate(), AName(), Today.AddDays(-1), MemberCategory.Adult,
                ACardNumber(), AContact(), guardian: null, Today)
            .HasErrors().ShouldBeFalse();

    // --- Entitlement -------------------------------------------------------------------------------

    [Fact]
    public void MembershipIsCurrent_OnBothEndsOfThePeriod_Inclusive()
    {
        var member = AMember();

        member.MembershipIsCurrentOn(member.MembershipStart).ShouldBeTrue();
        member.MembershipIsCurrentOn(member.MembershipEnd).ShouldBeTrue();
        member.MembershipIsCurrentOn(member.MembershipStart.AddDays(-1)).ShouldBeFalse();
        member.MembershipIsCurrentOn(member.MembershipEnd.AddDays(1)).ShouldBeFalse();
    }

    // --- Renewal -----------------------------------------------------------------------------------

    [Fact]
    public void Renew_BeforeExpiry_ExtendsFromTheOldEnd()
    {
        // Renewing early costs nothing, or members would learn to let memberships lapse first.
        var member = AMember().Settled();
        var oldEnd = member.MembershipEnd;

        member.Renew(oldEnd.AddMonths(-1));

        member.MembershipStart.ShouldBe(Today);
        member.MembershipEnd.ShouldBe(oldEnd.AddMonths(Member.MembershipDurationInMonths));
        member.Event<MembershipRenewed>().NewEnd.ShouldBe(member.MembershipEnd);
    }

    [Fact]
    public void Renew_OnTheExpiryDayItself_StillExtendsFromTheOldEnd()
    {
        // The end day is inside the period, so renewing on it is renewing before expiry.
        var member = AMember().Settled();
        var oldEnd = member.MembershipEnd;

        member.Renew(oldEnd);

        member.MembershipEnd.ShouldBe(oldEnd.AddMonths(Member.MembershipDurationInMonths));
    }

    [Fact]
    public void Renew_AfterExpiry_StartsANewPeriodToday()
    {
        // The gap is not billed and not back-dated: nobody was entitled during it, and the record
        // should agree. Three years late is still a renewal, never a re-enrollment.
        var member = AMember().Settled();
        var muchLater = member.MembershipEnd.AddYears(3);

        member.Renew(muchLater);

        member.MembershipStart.ShouldBe(muchLater);
        member.MembershipEnd.ShouldBe(muchLater.AddMonths(Member.MembershipDurationInMonths));
        member.Event<MembershipRenewed>().NewEnd.ShouldBe(member.MembershipEnd);
    }

    // --- Category ----------------------------------------------------------------------------------

    [Fact]
    public void ChangeCategory_SaysSo()
    {
        var member = AMember().Settled();

        member.ChangeCategory(MemberCategory.Student).HasErrors().ShouldBeFalse();

        var changed = member.Event<MemberCategoryChanged>();
        changed.PreviousCategory.ShouldBe(MemberCategory.Adult);
        changed.NewCategory.ShouldBe(MemberCategory.Student);
    }

    [Fact]
    public void ChangeCategory_ToTheSameCategory_RecordsNothing()
    {
        var member = AMember().Settled();

        member.ChangeCategory(MemberCategory.Adult).HasErrors().ShouldBeFalse();

        member.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ChangeCategory_ToChildWithoutAGuardian_IsRefused()
    {
        var member = AMember().Settled();

        var outcome = member.ChangeCategory(MemberCategory.Child);

        outcome.HasErrors().ShouldBeTrue();
        member.Category.ShouldBe(MemberCategory.Adult);
    }

    [Fact]
    public void ChangeCategory_LeavingChild_KeepsTheGuardian()
    {
        // Changing what a member may do does not change who is reached.
        var member = AMember(MemberCategory.Child, AGuardian()).Settled();

        member.ChangeCategory(MemberCategory.Adult).HasErrors().ShouldBeFalse();

        member.Guardian.ShouldNotBeNull();
    }

    // --- The card ----------------------------------------------------------------------------------

    [Fact]
    public void ReplaceCard_SaysSo_AndCarriesTheRetiredNumber()
    {
        var member = AMember().Settled();
        var previous = member.CardNumber;

        member.ReplaceCard(ACardNumber("20260000999"));

        var replaced = member.Event<CardReplaced>();
        replaced.PreviousCardNumber.ShouldBe(previous);
        replaced.NewCardNumber.ShouldBe(member.CardNumber);
    }

    [Fact]
    public void ReplaceCard_WithTheNumberItAlreadyCarries_RecordsNothing()
    {
        var member = AMember().Settled();

        member.ReplaceCard(member.CardNumber!);

        member.DomainEvents.ShouldBeEmpty();
    }

    // --- Contact and guardian ----------------------------------------------------------------------

    [Fact]
    public void UpdateContactDetails_SaysSo_AndClearingEveryChannelIsLegitimate()
    {
        var member = AMember().Settled();

        member.UpdateContactDetails(ContactDetails.None);

        member.Event<ContactDetailsChanged>().NewContactDetails.ShouldBe(ContactDetails.None);
    }

    [Fact]
    public void UpdateContactDetails_ToTheSameChannels_RecordsNothing()
    {
        var member = AMember().Settled();

        member.UpdateContactDetails(AContact());

        member.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ChangeGuardian_OfAChild_ToNobody_IsRefused()
    {
        var member = AMember(MemberCategory.Child, AGuardian()).Settled();

        var outcome = member.ChangeGuardian(null);

        outcome.HasErrors().ShouldBeTrue();
        member.Guardian.ShouldNotBeNull();
    }

    [Fact]
    public void ChangeGuardian_OfAnAdult_ToNobody_SaysSo()
    {
        var member = AMember(MemberCategory.Adult, AGuardian()).Settled();

        member.ChangeGuardian(null).HasErrors().ShouldBeFalse();

        member.Guardian.ShouldBeNull();
        member.Event<GuardianChanged>().NewGuardian.ShouldBeNull();
    }

    [Fact]
    public void ChangeGuardian_ToTheSameGuardian_RecordsNothing()
    {
        var member = AMember(MemberCategory.Child, AGuardian()).Settled();

        member.ChangeGuardian(AGuardian()).HasErrors().ShouldBeFalse();

        member.DomainEvents.ShouldBeEmpty();
    }

    // --- Renaming ----------------------------------------------------------------------------------

    [Fact]
    public void Rename_SaysSo_AndCarriesBothNames()
    {
        var member = AMember().Settled();
        var previous = member.Name;

        member.Rename(AName("Antoine", "Doinel-Montag"));

        var renamed = member.Event<MemberRenamed>();
        renamed.PreviousName.ShouldBe(previous);
        renamed.NewName.ShouldBe(member.Name);
    }

    [Fact]
    public void Rename_ToTheCurrentName_RecordsNothing()
    {
        var member = AMember().Settled();

        member.Rename(AName());

        member.DomainEvents.ShouldBeEmpty();
    }

    // --- Erasure -----------------------------------------------------------------------------------

    [Fact]
    public void Erase_EmptiesTheRecord_AndKeepsWhatIdentifiesNobody()
    {
        var member = AMember(MemberCategory.Child, AGuardian()).Settled();
        var membershipEnd = member.MembershipEnd;

        member.Erase(Today).HasErrors().ShouldBeFalse();

        member.Name.ShouldBeNull();
        member.DateOfBirth.ShouldBeNull();
        member.CardNumber.ShouldBeNull();
        member.ContactDetails.ShouldBe(ContactDetails.None);
        member.Guardian.ShouldBeNull();
        member.ErasedOn.ShouldBe(Today);

        // What identifies nobody stays: the identifier downstream contexts hold, the category and
        // the membership span the statistics are argued from.
        member.MembershipEnd.ShouldBe(membershipEnd);
        member.Category.ShouldBe(MemberCategory.Child);
        member.Event<MemberErased>().MemberId.ShouldBe(member.Id);
    }

    [Fact]
    public void Erase_AChild_TakesTheGuardianWithIt()
    {
        // The invariant 'a child always has a guardian' protects reaching a minor who can still
        // act; an erased member acts no more, and an invariant whose reason has ended ends with
        // it — the Withdrawn pattern, on a person.
        var member = AMember(MemberCategory.Child, AGuardian()).Settled();

        member.Erase(Today).HasErrors().ShouldBeFalse();

        member.Guardian.ShouldBeNull();
    }

    [Fact]
    public void Erase_Twice_IsErasingOnce()
    {
        var member = AMember().Settled();
        member.Erase(Today);
        member.ClearDomainEvents();

        member.Erase(Today.AddDays(1)).HasErrors().ShouldBeFalse();

        member.DomainEvents.ShouldBeEmpty();
        member.ErasedOn.ShouldBe(Today);
    }

    [Fact]
    public void AnErasedMember_ActsNoMore()
    {
        var member = AMember().Settled();
        member.Erase(Today);

        List<Result> refused =
        [
            member.Renew(Today),
            member.ChangeCategory(MemberCategory.Student),
            member.ReplaceCard(ACardNumber("20260000999")),
            member.UpdateContactDetails(AContact()),
            member.ChangeGuardian(AGuardian()),
            member.Rename(AName()),
        ];

        foreach (var outcome in refused)
        {
            outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList())
                .ShouldContain(MembersErrorCodes.MemberErased);
        }
    }

    // --- Merged away ------------------------------------------------------------------------------

    [Fact]
    public void AMergedMember_ActsNoMore_AndTheRefusalSaysSoInItsOwnWords()
    {
        // Two refusals and not one, because a member of staff does different things with them: an
        // erasure is the end of a relationship and there is nowhere to go, while a merge means the
        // person is here, under the other record.
        var member = AMember().Settled();
        new MemberMergeDomainService().Merge(member, AMember());

        List<Result> refused =
        [
            member.Renew(Today),
            member.ChangeCategory(MemberCategory.Student),
            member.ReplaceCard(ACardNumber("20260000999")),
            member.UpdateContactDetails(AContact()),
            member.ChangeGuardian(AGuardian()),
            member.Rename(AName("Colette", "Tazzi")),
        ];

        foreach (var outcome in refused)
        {
            outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList())
                .ShouldContain(MembersErrorCodes.MemberMerged);
        }
    }

    [Fact]
    public void AMergedMember_KeepsEveryFieldItHad()
    {
        // A merge does not anonymize. An audit has to be able to read which two records were judged
        // one person, and by which name — and the card stops opening anything by the entitlement
        // answering "unknown", not by being wiped.
        var member = AMember().Settled();

        new MemberMergeDomainService().Merge(member, AMember());

        member.Name.ShouldBe(AName());
        member.CardNumber.ShouldBe(ACardNumber());
        member.ContactDetails.ShouldBe(AContact());
        member.DateOfBirth.ShouldBe(ABirthDate);
    }

    [Fact]
    public void AMergedMember_CanStillBeErased()
    {
        // The one act a merged record accepts, and the exception is the point: the merge left a
        // name, a card and an address in place, and a person's right to be forgotten does not stop
        // at the record somebody judged the secondary one.
        var member = AMember().Settled();
        new MemberMergeDomainService().Merge(member, AMember());
        member.ClearDomainEvents();

        member.Erase(Today).HasErrors().ShouldBeFalse();

        member.ErasedOn.ShouldBe(Today);
        member.Name.ShouldBeNull();
        member.CardNumber.ShouldBeNull();
        member.ContactDetails.ShouldBe(ContactDetails.None);
        member.Event<MemberErased>().MemberId.ShouldBe(member.Id);

        // And it is still a pointer: the erasure took the person, not the fact that two files were
        // one.
        member.MergedInto.ShouldNotBeNull();
    }
}
