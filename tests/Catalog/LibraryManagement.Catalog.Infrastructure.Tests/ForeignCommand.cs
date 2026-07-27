using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Infrastructure.Tests;

// Stands in for a command belonging to a module that has not registered a unit of work. It
// is declared in this assembly rather than the Catalog one, which is exactly what makes it foreign:
// the module a command belongs to is the assembly that declares it.
public sealed record ForeignCommand : ICommand<Result>;
