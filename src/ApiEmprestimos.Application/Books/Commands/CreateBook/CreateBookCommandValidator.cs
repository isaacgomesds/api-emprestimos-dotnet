using FluentValidation;

namespace ApiEmprestimos.Application.Books.Commands.CreateBook;

public sealed class CreateBookCommandValidator : AbstractValidator<CreateBookCommand>
{
    public CreateBookCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(250);
        RuleFor(command => command.Isbn).NotEmpty().MaximumLength(20);
        RuleFor(command => command.Author).NotEmpty().MaximumLength(200);
        RuleFor(command => command.TotalCopies).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
        RuleFor(command => command.CorrelationId).NotEmpty().MaximumLength(100);
    }
}
