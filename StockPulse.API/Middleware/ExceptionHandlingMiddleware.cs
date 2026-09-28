using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using StockPulse.Application.Exceptions;

namespace StockPulse.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("Validation failed: {Errors}", ex.Errors.Select(e => e.ErrorMessage));
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/problem+json";

            var problem = new ValidationProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Validation failed",
                Detail = "One or more validation errors occurred."
            };

            foreach (var error in ex.Errors)
                problem.Errors.TryAdd(error.PropertyName, [error.ErrorMessage]);

            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning("Not found: {Message}", ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = (int)HttpStatusCode.NotFound,
                Title = "Resource not found",
                Detail = ex.Message
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred."
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
