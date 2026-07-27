using System.Reflection;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// The code an architecture rule is asked about: every LibraryManagement assembly this project
/// brings into its output directory, test assemblies excluded.
/// </summary>
/// <remarks>
/// <para>
/// Modules arrive as this project references them, so nobody has to remember to extend a list.
/// Forgetting would be the one failure an architecture rule cannot report by itself: green over
/// nothing reads exactly like green over everything, which is why each rule's tests also pin what
/// the scan found.
/// </para>
/// <para>
/// Loading by path rather than by name because these assemblies are read, not run — a rule needs
/// their metadata and nothing else. One consequence is worth knowing:
/// <see cref="Assembly.LoadFrom(string)"/> can hand back types that are not reference-equal to the
/// ones this project compiled against, so a test comparing a discovered type to a <c>typeof</c>
/// compares names.
/// </para>
/// </remarks>
public static class ProductionCode
{
    /// <summary>
    /// Returns the assemblies a rule should be applied to.
    /// </summary>
    public static Assembly[] Assemblies()
    {
        var directory = Path.GetDirectoryName(typeof(ProductionCode).Assembly.Location)!;

        return Directory
            .EnumerateFiles(directory, "LibraryManagement.*.dll")
            .Where(path => !Path.GetFileName(path).Contains(".Tests.", StringComparison.Ordinal))
            .Select(Assembly.LoadFrom)
            .ToArray();
    }

    /// <summary>
    /// The assembly holding the deliberate violations each rule is exercised against.
    /// </summary>
    public static Assembly Fixtures => typeof(ProductionCode).Assembly;
}
