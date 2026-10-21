using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiEmprestimos.Application.Loans.Commands.CreateLoan;

public sealed class CreateLoanCommandHandler(
    IBookRepository bookRepository,
    IUserRepository userRepository,
    ILoanRepository loanRepository,
    IIdempotentRequestRepository idempotentRequestRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateLoanCommand, LoanResponseDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<LoanResponseDto> Handle(
        CreateLoanCommand request,
        CancellationToken cancellationToken)
    {
        var requestHash = CreateRequestHash(request);
        return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var idempotentRequest = IdempotentRequest.CreatePending(request.IdempotencyKey, requestHash);
            if (!await idempotentRequestRepository.TryCreateAsync(idempotentRequest, transactionCancellationToken))
            {
                var existingRequest = await idempotentRequestRepository.GetByKeyAsync(
                    request.IdempotencyKey,
                    transactionCancellationToken)
                    ?? throw new InvalidOperationException("The idempotency record could not be loaded.");

                if (!string.Equals(existingRequest.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The Idempotency-Key has already been used with a different request.");
                }

                return JsonSerializer.Deserialize<LoanResponseDto>(existingRequest.ResponseBody, JsonOptions)
                    ?? throw new InvalidOperationException("The stored idempotent response is invalid.");
            }

            var book = await bookRepository.GetByIdAsync(request.BookId, transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Book '{request.BookId}' was not found.");
            if (await userRepository.GetByIdAsync(request.UserId, transactionCancellationToken) is null)
            {
                throw new KeyNotFoundException($"User '{request.UserId}' was not found.");
            }

            if (await loanRepository.HasActiveLoanAsync(
                    request.UserId,
                    request.BookId,
                    transactionCancellationToken))
            {
                throw new InvalidOperationException("The user already has an active loan for this book.");
            }

            if (!await bookRepository.TryReserveCopyAsync(book.Id, transactionCancellationToken))
            {
                throw new InvalidOperationException("The book is inactive or has no available copies.");
            }

            var loan = Loan.Create(request.BookId, request.UserId, request.LoanDateUtc, request.DueDateUtc);
            await loanRepository.RegisterAsync(loan, transactionCancellationToken);
            await auditEventRepository.LogAsync(
                AuditEventFactory.Create(
                    nameof(Loan),
                    loan.Id,
                    "LoanCreated",
                    request.Actor,
                    request.CorrelationId,
                    new { loan.BookId, loan.UserId, loan.LoanDateUtc, loan.DueDateUtc }),
                transactionCancellationToken);
            await loanRepository.SaveChangesAsync(transactionCancellationToken);

            var loanResponse = new LoanResponseDto(
                loan.Id,
                loan.BookId,
                loan.UserId,
                loan.LoanDateUtc,
                loan.DueDateUtc,
                loan.ReturnDateUtc,
                loan.Status);

            idempotentRequest.Complete(
                201,
                JsonSerializer.Serialize(loanResponse, JsonOptions));
            await idempotentRequestRepository.SaveChangesAsync(transactionCancellationToken);

            return loanResponse;
        }, cancellationToken);
    }

    private static string CreateRequestHash(CreateLoanCommand request)
    {
        var canonicalRequest = JsonSerializer.Serialize(new
        {
            request.BookId,
            request.UserId,
            request.LoanDateUtc,
            request.DueDateUtc
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
    }
}
