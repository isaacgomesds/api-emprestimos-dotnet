using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using ApiEmprestimos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiEmprestimos.Infrastructure.Repositories;

public sealed class IdempotentRequestRepository(LibraryDbContext dbContext) : IIdempotentRequestRepository
{
    public async Task<bool> TryCreateAsync(
        IdempotentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var insertedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "IdempotentRequests" ("Id", "Key", "RequestHash", "StatusCode", "ResponseBody", "CreatedAtUtc")
            VALUES ({request.Id}, {request.Key}, {request.RequestHash}, {request.StatusCode}, {request.ResponseBody}, {request.CreatedAtUtc})
            ON CONFLICT ("Key") DO NOTHING
            """,
            cancellationToken);

        if (insertedRows == 1)
        {
            dbContext.IdempotentRequests.Attach(request);
            return true;
        }

        return false;
    }

    public Task<IdempotentRequest?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        dbContext.IdempotentRequests.SingleOrDefaultAsync(
            request => request.Key == key,
            cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
