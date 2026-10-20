using FluentValidation;

namespace ApiEmprestimos.Application.Users.Commands.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Actor).NotEmpty().MaximumLength(200);
        RuleFor(command => command.CorrelationId).NotEmpty().MaximumLength(100);
    }
}
