using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Application.Users.Commands.CreateUser;
using ApiEmprestimos.Application.Users.Queries.GetAllUsers;
using ApiEmprestimos.Application.Users.Queries.GetUserLoanHistory;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ApiEmprestimos.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users").WithTags("Users");

        group.MapGet("/", async Task<Ok<IReadOnlyList<UserResponseDto>>> (
                ISender sender,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(new GetAllUsersQuery(), cancellationToken)))
            .WithName("GetAllUsers")
            .WithSummary("Get all users");

        group.MapPost("/", async Task<Created<UserResponseDto>> (
                CreateUserRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var user = await sender.Send(
                    new CreateUserCommand(
                        request.Name,
                        request.Email,
                        RequestMetadata.GetActor(httpContext),
                        RequestMetadata.GetCorrelationId(httpContext)),
                    cancellationToken);
                return TypedResults.Created($"/users/{user.Id}", user);
            })
            .WithName("CreateUser")
            .WithSummary("Create a user")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/loans", async Task<Ok<IReadOnlyList<UserLoanHistoryResponseDto>>> (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(new GetUserLoanHistoryQuery(id), cancellationToken)))
            .WithName("GetUserLoanHistory")
            .WithSummary("Get a user's loan history")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
