using ApiEmprestimos.Application.Books.Commands.CreateBook;
using ApiEmprestimos.Application.Books.Commands.DeactivateBook;
using ApiEmprestimos.Application.Books.Commands.UpdateBook;
using ApiEmprestimos.Application.Books.Queries.GetBookById;
using ApiEmprestimos.Application.Books.Queries.ListActiveBooks;
using ApiEmprestimos.Application.Common.DTOs;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ApiEmprestimos.Api.Endpoints;

public static class BookEndpoints
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/books").WithTags("Books");

        group.MapPost("/", async Task<Created<BookResponseDto>> (
                CreateBookRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var book = await sender.Send(
                    new CreateBookCommand(
                        request.Title,
                        request.Isbn,
                        request.Author,
                        request.TotalCopies,
                        RequestMetadata.GetActor(httpContext),
                        RequestMetadata.GetCorrelationId(httpContext)),
                    cancellationToken);
                return TypedResults.Created($"/books/{book.Id}", book);
            })
            .WithName("CreateBook")
            .WithSummary("Create a book")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async Task<Ok<IReadOnlyList<BookResponseDto>>> (
                ISender sender,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(new ListActiveBooksQuery(), cancellationToken)))
            .WithName("ListActiveBooks")
            .WithSummary("List active books");

        group.MapGet("/{id:guid}", async Task<Ok<BookResponseDto>> (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(new GetBookByIdQuery(id), cancellationToken)))
            .WithName("GetBookById")
            .WithSummary("Get a book by ID")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", async Task<Ok<BookResponseDto>> (
                Guid id,
                UpdateBookRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(
                new UpdateBookCommand(
                    id,
                    request.Title,
                    request.Author,
                    request.TotalCopies,
                    RequestMetadata.GetActor(httpContext),
                    RequestMetadata.GetCorrelationId(httpContext)),
                cancellationToken)))
            .WithName("UpdateBook")
            .WithSummary("Partially update a book")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async Task<NoContent> (
                Guid id,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(
                    new DeactivateBookCommand(
                        id,
                        RequestMetadata.GetActor(httpContext),
                        RequestMetadata.GetCorrelationId(httpContext)),
                    cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("DeactivateBook")
            .WithSummary("Deactivate a book")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
