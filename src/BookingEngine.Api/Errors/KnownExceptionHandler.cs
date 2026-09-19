using BookingEngine.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingEngine.Api.Errors;

public sealed class KnownExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Code, string Detail)? problem = exception switch
        {
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "domain.rule_violation", e.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "resource.duplicate", "A resource with the same unique values already exists."),
            _ => null,
        };

        if (problem is not { } p)
        {
            return false;
        }

        httpContext.Response.StatusCode = p.Status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = p.Status,
                Detail = p.Detail,
                Extensions = { ["code"] = p.Code },
            },
        });
    }
}
