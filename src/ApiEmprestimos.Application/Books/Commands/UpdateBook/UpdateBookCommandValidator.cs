using FluentValidation;

namespace ApiEmprestimos.Application.Books.Commands.UpdateBook;

public sealed class UpdateBookCommandValidator : AbstractValidator<UpdateBookCommand>
{
    public UpdateBookCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(250).When(command => command.Title is not null);
        RuleFor(command => command.Author).NotEmpty().MaximumLength(200).When(command => command.Author is not null);
        RuleFor(command => command.TotalCopies).GreaterThanOrEqualTo(0).When(command => command.TotalCopies is not null);
        RuleFor(command => command)
            .Must(command => command.Title is not null
                || command.Author is not null
                || command.TotalCopies is not null)
            .WithMessage("At least one book property must be provided.");
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
        RuleFor(command => command.CorrelationId).NotEmpty().MaximumLength(100);
    }
}
