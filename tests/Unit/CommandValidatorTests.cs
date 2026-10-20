using ApiEmprestimos.Application.Books.Commands.CreateBook;
using ApiEmprestimos.Application.Loans.Commands.CreateLoan;
using ApiEmprestimos.Application.Users.Commands.CreateUser;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class CommandValidatorTests
{
    [Fact]
    public void Create_book_validator_rejects_empty_fields_and_negative_stock()
    {
        var command = new CreateBookCommand("", "", "", -1, "", "");

        var result = new CreateBookCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(["Title", "Isbn", "Author", "TotalCopies", "Actor", "CorrelationId"]);
    }

    [Fact]
    public void Create_loan_validator_rejects_missing_identifiers_and_invalid_dates()
    {
        var command = new CreateLoanCommand(
            Guid.Empty,
            Guid.Empty,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-1),
            "",
            "",
            "");

        var result = new CreateLoanCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(["BookId", "UserId", "DueDateUtc", "IdempotencyKey", "Actor", "CorrelationId"]);
    }

    [Fact]
    public void Create_user_validator_rejects_invalid_email_and_required_metadata()
    {
        var command = new CreateUserCommand("", "not-an-email", "", "");

        var result = new CreateUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(["Name", "Email", "Actor", "CorrelationId"]);
    }
}
