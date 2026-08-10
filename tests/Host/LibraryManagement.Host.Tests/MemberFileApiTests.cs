using System.Net;
using LibraryManagement.Host.Http;
using Microsoft.Data.SqlClient;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// The composed member file over the wire.
/// </summary>
/// <remarks>
/// What the composition itself does is settled without a database in
/// <see cref="MemberFileCompositionTests"/>. What is new here is the wiring: that the route exists,
/// that all three modules answer inside one request against one real database, and that a member
/// with nothing out reads as an empty file rather than as a missing one.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MemberFileApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TheHost _host = null!;
    private HttpClient _client = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_MemberFile_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await Databases.DropAsync(ConnectionString, Token);
        _host = new TheHost(ConnectionString);
        _client = _host.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _host.DisposeAsync();
    }

    private async Task<Guid> AnEnrolledMemberAsync()
    {
        var memberId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(
            "/members",
            new
            {
                MemberId = memberId,
                GivenName = "Antoine",
                FamilyName = "Doinel",
                DateOfBirth = new DateOnly(1990, 5, 1),
                Category = "Adult",
                CardNumber = Guid.NewGuid().ToString("N")[..12],
                Email = "antoine.doinel@example.org",
            },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        return memberId;
    }

    private Task<HttpResponseMessage> GetFileAsync(Guid memberId)
        => _client.GetAsync(new Uri($"/members/{memberId}/file", UriKind.Relative), Token);

    [Fact]
    public async Task AMemberWithNothingOut_ReadsAsAWholeFile()
    {
        var memberId = await AnEnrolledMemberAsync();

        var response = await GetFileAsync(memberId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var file = (await response.Content.ReadFromJsonAsync<MemberFileResponse>(Token)).ShouldNotBeNull();

        file.Member.MemberId.ShouldBe(memberId);
        file.Member.GivenName.ShouldBe("Antoine");
        file.Member.Category.ShouldBe("Adult");

        // Empty, not absent. Circulation keeps no register of people and an account opens with the
        // first charge, so both modules answer for a member they have never heard of — which is
        // what keeps the ordinary case off the degraded path.
        file.Circulation.ShouldNotBeNull().Loans.ShouldBeEmpty();
        file.Circulation.Holds.ShouldBeEmpty();
        file.Circulation.DebtBlocksBorrowing.ShouldBeFalse();
        file.Charges.ShouldNotBeNull().Balance.ShouldBe(0m);
        file.Unavailable.ShouldBeEmpty();
    }

    [Fact]
    public async Task AMemberNobodyEnrolled_IsA404()
    {
        // A query addresses a thing, so a missing one is exactly a 404 — and this is the one query
        // of the three whose failure the caller is meant to see.
        (await GetFileAsync(Guid.CreateVersion7())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
