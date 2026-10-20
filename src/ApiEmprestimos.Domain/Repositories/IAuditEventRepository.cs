using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Domain.Repositories;

public interface IAuditEventRepository
{
    Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
