using FluentValidation;

namespace ApiEmprestimos.Application.Loans.Commands.CreateLoan;

public sealed class CreateLoanCommandValidator : AbstractValidator<CreateLoanCommand>
{
    public CreateLoanCommandValidator()
    {
        RuleFor(command => command.BookId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.LoanDateUtc).NotEmpty();
        RuleFor(command => command.DueDateUtc).NotEmpty();
        RuleFor(command => command.DueDateUtc).GreaterThanOrEqualTo(command => command.LoanDateUtc);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
        RuleFor(command => command.CorrelationId).NotEmpty().MaximumLength(100);
    }
}
