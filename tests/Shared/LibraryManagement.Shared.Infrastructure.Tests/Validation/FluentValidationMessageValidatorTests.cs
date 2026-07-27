using FluentValidation;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;
using LibraryManagement.Shared.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Tests.Validation;

public sealed class FluentValidationMessageValidatorTests
{
    // A command with fields worth checking. The rules below are about shape only — no library rule
    // appears here, those belong to the domain.
    public sealed record RegisterBookCommand(string Isbn, string Title) : ICommand<Result>;

    public sealed class RegisterBookCommandValidator : AbstractValidator<RegisterBookCommand>
    {
        public RegisterBookCommandValidator()
        {
            RuleFor(command => command.Isbn).NotEmpty().Length(13);
            RuleFor(command => command.Title).NotEmpty();
        }
    }

    // A second validator over the same command, to show they are all consulted.
    public sealed class RegisterBookCommandTrimValidator : AbstractValidator<RegisterBookCommand>
    {
        public RegisterBookCommandTrimValidator()
            => RuleFor(command => command.Title)
                .Must(title => title is null || title.Trim() == title)
                .WithMessage("must not be padded with spaces");
    }

    // Carries a message no default rule would produce, so a test can tell the adapter preserved it
    // rather than matching FluentValidation's own wording — which is not ours to depend on.
    public sealed class RegisterBookCommandCustomMessageValidator : AbstractValidator<RegisterBookCommand>
    {
        public const string Message = "an ISBN is printed on the back cover, above the barcode";

        public RegisterBookCommandCustomMessageValidator()
            => RuleFor(command => command.Isbn).NotEmpty().WithMessage(Message);
    }

    private static FluentValidationMessageValidator ValidatorWith(params Action<IServiceCollection>[] registrations)
    {
        var services = new ServiceCollection();
        foreach (var register in registrations)
        {
            register(services);
        }

        return new FluentValidationMessageValidator(services.BuildServiceProvider());
    }

    private static FluentValidationMessageValidator WithTheBookValidator()
        => ValidatorWith(services =>
            services.AddScoped<IValidator<RegisterBookCommand>, RegisterBookCommandValidator>());

    [Fact]
    public async Task ValidateAsync_WhenTheCommandIsWellFormed_ReportsNothing()
    {
        var errors = await WithTheBookValidator().ValidateAsync(
            new RegisterBookCommand("9780321125217", "Domain-Driven Design"),
            TestContext.Current.CancellationToken);

        errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenAFieldIsWrong_NamesItInTheTarget()
    {
        var errors = await WithTheBookValidator().ValidateAsync(
            new RegisterBookCommand("too-short", "Domain-Driven Design"),
            TestContext.Current.CancellationToken);

        errors.ShouldHaveSingleItem().Target.ShouldBe(nameof(RegisterBookCommand.Isbn));
    }

    [Fact]
    public async Task ValidateAsync_UsesTheSharedValidationCodeRatherThanOnePerField()
    {
        var errors = await WithTheBookValidator().ValidateAsync(
            new RegisterBookCommand("too-short", string.Empty),
            TestContext.Current.CancellationToken);

        errors.Select(error => error.ErrorCode)
            .Distinct()
            .ShouldHaveSingleItem()
            .ShouldBe(SharedErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task ValidateAsync_ReportsEveryOffendingFieldAtOnce()
    {
        var errors = await WithTheBookValidator().ValidateAsync(
            new RegisterBookCommand(string.Empty, string.Empty),
            TestContext.Current.CancellationToken);

        errors.Select(error => error.Target)
            .Distinct()
            .ShouldBe([nameof(RegisterBookCommand.Isbn), nameof(RegisterBookCommand.Title)], ignoreOrder: true);
    }

    [Fact]
    public async Task ValidateAsync_KeepsTheMessageWrittenByTheRule()
    {
        var validator = ValidatorWith(services =>
            services.AddScoped<IValidator<RegisterBookCommand>, RegisterBookCommandCustomMessageValidator>());

        var errors = await validator.ValidateAsync(
            new RegisterBookCommand(string.Empty, "Domain-Driven Design"),
            TestContext.Current.CancellationToken);

        errors.ShouldHaveSingleItem()
            .ErrorMessage.ShouldBe(RegisterBookCommandCustomMessageValidator.Message);
    }

    [Fact]
    public async Task ValidateAsync_ConsultsEveryValidatorRegisteredForTheCommand()
    {
        var validator = ValidatorWith(
            services => services.AddScoped<IValidator<RegisterBookCommand>, RegisterBookCommandValidator>(),
            services => services.AddScoped<IValidator<RegisterBookCommand>, RegisterBookCommandTrimValidator>());

        var errors = await validator.ValidateAsync(
            new RegisterBookCommand("9780321125217", "  padded  "),
            TestContext.Current.CancellationToken);

        errors.ShouldContain(error => error.ErrorMessage == "must not be padded with spaces");
    }

    [Fact]
    public async Task ValidateAsync_WhenNoValidatorIsRegistered_ReportsNothing()
    {
        // An absent validator means nothing about this command's shape is worth checking, not that
        // someone forgot. Requiring one per command is an architecture test's job.
        var errors = await ValidatorWith().ValidateAsync(
            new RegisterBookCommand(string.Empty, string.Empty),
            TestContext.Current.CancellationToken);

        errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_IgnoresAValidatorRegisteredForADifferentCommand()
    {
        var validator = ValidatorWith(
            services => services.AddScoped<IValidator<RegisterBookCommand>, RegisterBookCommandValidator>());

        var errors = await validator.ValidateAsync(new TestCommand(), TestContext.Current.CancellationToken);

        errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsASnapshotDetachedFromAnyAccumulator()
    {
        var errors = await WithTheBookValidator().ValidateAsync(
            new RegisterBookCommand(string.Empty, "Domain-Driven Design"),
            TestContext.Current.CancellationToken);

        (errors is IErrorCollection).ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithANullCommand_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await WithTheBookValidator()
                .ValidateAsync(null!, TestContext.Current.CancellationToken));
    }
}
