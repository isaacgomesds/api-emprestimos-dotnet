using System.Reflection;
using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ApiEmprestimos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton<LoanMetrics>();
        services.AddSingleton<ILoanMetrics>(serviceProvider =>
            serviceProvider.GetRequiredService<LoanMetrics>());

        return services;
    }
}
