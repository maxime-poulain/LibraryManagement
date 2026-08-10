namespace LibraryManagement.Host.Auditing;

/// <summary>
/// Reads the employee header off a request and hands it to the scope's
/// <see cref="HeaderEmployee"/>.
/// </summary>
/// <param name="next">The rest of the pipeline.</param>
/// <remarks>
/// A request without the header is not refused. The audit columns then record that nobody claimed
/// the change, which is the truth — and refusing here would put an access rule in a host that
/// verifies nothing, giving the appearance of a gate where there is none.
/// </remarks>
public sealed class EmployeeHeaderMiddleware(RequestDelegate next)
{
    /// <summary>The header a caller names itself in.</summary>
    /// <remarks>
    /// A constant rather than configuration: nothing about it varies per environment, and a
    /// configurable name would be a knob with no turner.
    /// </remarks>
    public const string HeaderName = "X-Employee-Id";

    /// <summary>
    /// Runs the middleware.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="employee">The scope's employee, filled here and read by the audit interceptor.</param>
    /// <returns>A task that completes when the rest of the pipeline has.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    public Task InvokeAsync(HttpContext context, HeaderEmployee employee)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(employee);

        if (context.Request.Headers.TryGetValue(HeaderName, out var claimed)
            && !string.IsNullOrWhiteSpace(claimed))
        {
            employee.Identify(claimed.ToString());
        }

        return next(context);
    }
}
