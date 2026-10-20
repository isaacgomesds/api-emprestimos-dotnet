using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Application.Loans.Commands.CancelLoan;
using ApiEmprestimos.Application.Loans.Commands.CreateLoan;
using ApiEmprestimos.Application.Loans.Commands.ReturnLoan;
using ApiEmprestimos.Application.Loans.Queries.GetBookLoanHistory;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ApiEmprestimos.Api.Endpoints;

public static class LoanEndpoints
{
    public static IEndpointRouteBuilder MapLoanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var loans = endpoints.MapGroup("/loans").WithTags("Loans");
        loans.MapPost("/", async Task<Created<LoanResponseDto>> (
                CreateLoanRequest request,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var normalizedIdempotencyKey = idempotencyKey?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedIdempotencyKey))
                {
                    throw new ArgumentException("The Idempotency-Key header is required.");
                }

                if (normalizedIdempotencyKey.Length > 200)
                {
                    throw new ArgumentException("The Idempotency-Key header cannot exceed 200 characters.");
                }

                var loan = await sender.Send(
                    new CreateLoanCommand(
                        request.BookId,
                        request.UserId,
                        request.LoanDateUtc.UtcDateTime,
                        request.DueDateUtc.UtcDateTime,
                        normalizedIdempotencyKey,
                        RequestMetadata.GetActor(httpContext),
                        RequestMetadata.GetCorrelationId(httpContext)),
                    cancellationToken);
                return TypedResults.Created($"/loans/{loan.Id}", loan);
            })
            .WithName("CreateLoan")
            .WithSummary("Create a loan")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithDescription("Requires an Idempotency-Key header to identify the request.");

        loans.MapPost("/{id:guid}/return", async Task<Ok<LoanResponseDto>> (
                Guid id,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(
                new ReturnLoanCommand(
                    id,
                    RequestMetadata.GetActor(httpContext),
                    RequestMetadata.GetCorrelationId(httpContext)),
                cancellationToken)))
            .WithName("ReturnLoan")
            .WithSummary("Return a loan")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        loans.MapPost("/{id:guid}/cancel", async Task<Ok<LoanResponseDto>> (
                Guid id,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(
                new CancelLoanCommand(
                    id,
                    RequestMetadata.GetActor(httpContext),
                    RequestMetadata.GetCorrelationId(httpContext)),
                cancellationToken)))
            .WithName("CancelLoan")
            .WithSummary("Cancel a loan")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var books = endpoints.MapGroup("/books").WithTags("Loans");
        books.MapGet("/{id:guid}/history", async Task<Ok<IReadOnlyList<LoanResponseDto>>> (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(new GetBookLoanHistoryQuery(id), cancellationToken)))
            .WithName("GetBookLoanHistory")
            .WithSummary("Get a book's loan history")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
