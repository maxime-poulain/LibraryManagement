using LibraryManagement.Shared.Application;

namespace LibraryManagement.Host.Auditing;

/// <summary>
/// The employee a request says it is acting for, read from a header.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is attribution, never authentication.</strong> Staff Access is a generic subdomain
/// the strategic design puts outside the model, and nothing here verifies the claim: a caller says
/// who they are and the audit columns believe them. That is honest for a system whose only client
/// today is the library's own desk software on the library's own network, and it is exactly what
/// must be replaced before the host meets anything else. The header is where a real identity will
/// arrive from too, so what changes that day is this class, not the audit interceptor and not a
/// single module.
/// </para>
/// <para>
/// Scoped and filled by the middleware rather than reaching into an <c>IHttpContextAccessor</c>: the
/// audit interceptor is already scoped and already asks for <see cref="ICurrentEmployee"/>, so the
/// request pipeline putting the value in is one arrow instead of an ambient lookup that works from
/// anywhere and is therefore reachable from everywhere.
/// </para>
/// <para>
/// Unattributed until told otherwise, which is what a Hangfire job gets: the daily run and the
/// drains act on nobody's behalf, and <c>null</c> is the truthful answer there rather than a name
/// the host invented.
/// </para>
/// </remarks>
public sealed class HeaderEmployee : ICurrentEmployee
{
    /// <inheritdoc/>
    public string? EmployeeId { get; private set; }

    /// <summary>
    /// Records who this request acts for.
    /// </summary>
    /// <param name="employeeId">The identifier the request carried.</param>
    public void Identify(string employeeId) => EmployeeId = employeeId;
}
