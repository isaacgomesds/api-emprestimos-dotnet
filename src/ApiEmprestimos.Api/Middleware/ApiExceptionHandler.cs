using ApiEmprestimos.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;

namespace ApiEmprestimos.Api.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isDuplicateIsbn = exception is BookIsbnAlreadyExistsException
            || IsBookIsbnUniqueViolation(exception);
        var (statusCode, title) = exception switch
        {
            BookIsbnAlreadyExistsException => (StatusCodes.Status409Conflict, "Conflict"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
            DbUpdateException when isDuplicateIsbn => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (0, string.Empty)
        };

        if (statusCode == 0)
        {
            return false;
        }

        logger.LogWarning(exception, "Request failed with {StatusCode}: {Title}", statusCode, title);
        httpContext.Response.StatusCode = statusCode;

        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

            httpContext.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                httpContext.Response.Body,
                new ValidationProblemDetails(errors)
                {
                    Status = statusCode,
                    Title = title,
                    Instance = httpContext.Request.Path
                },
                JsonSerializerOptions.Web,
                cancellationToken);
            return true;
        }

        httpContext.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = isDuplicateIsbn
                    ? "A book with this ISBN already exists."
                    : statusCode == StatusCodes.Status409Conflict ? exception.Message : title,
                Instance = httpContext.Request.Path
            },
            JsonSerializerOptions.Web,
            cancellationToken);
        return true;
    }

    private static bool IsBookIsbnUniqueViolation(Exception exception) =>
        exception is DbUpdateException
        && exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Books_Isbn"
        };
}
