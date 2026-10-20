using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using ApiEmprestimos.Infrastructure.Data;

namespace ApiEmprestimos.Infrastructure.Repositories;

public sealed class AuditEventRepository(LibraryDbContext dbContext) : IAuditEventRepository
{
    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        await dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
