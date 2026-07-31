using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.RelabelCopy;

/// <summary>
/// Replaces a copy's label.
/// </summary>
/// <param name="CopyId">The copy. Its identity does not change, which is the point.</param>
/// <param name="Barcode">The label from now on.</param>
/// <remarks>
/// An ordinary afternoon's work at a service desk: labels peel off, tear and stop scanning. The
/// barcode is a current fact about the object and never its identity — were it the identity,
/// relabelling would produce a different copy and orphan every loan ever made against the old one.
/// </remarks>
public sealed record RelabelCopyCommand(Guid CopyId, string Barcode) : ICommand<Result>;
