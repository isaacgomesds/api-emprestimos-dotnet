using ApiEmprestimos.Domain.Repositories;
using ApiEmprestimos.Infrastructure.Data;
using ApiEmprestimos.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ApiEmprestimos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
