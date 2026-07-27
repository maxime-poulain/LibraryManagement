using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

public sealed record TestCommand : ICommand<Result>;

public sealed record TestQuery(string Term) : IQuery<string>;
