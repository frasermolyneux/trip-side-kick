using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Domain.Common;

namespace MX.TripSideKick.Web.ExceptionHandling;

/// <summary>
/// Translates application/domain exceptions into <c>ProblemDetails</c> responses, so controllers
/// stay thin and never need their own try/catch blocks for the shared cases below.
/// </summary>
/// <remarks>
/// <see cref="NotFoundException"/> is deliberately also used for "you have no membership on this
/// trip" (see its remarks) - both map to 404 here, so a non-member can't distinguish "trip doesn't
/// exist" from "exists but you're not on it" by response code alone.
/// </remarks>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private readonly IProblemDetailsService problemDetailsService = problemDetailsService
        ?? throw new ArgumentNullException(nameof(problemDetailsService));

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ForbiddenException => StatusCodes.Status403Forbidden,
            InvitationIdentityMismatchException => StatusCodes.Status403Forbidden,
            ConcurrencyConflictException => StatusCodes.Status409Conflict,
            AlreadyMemberException => StatusCodes.Status409Conflict,
            LastOwnerViolationException => StatusCodes.Status409Conflict,
            InvitationStateException => StatusCodes.Status409Conflict,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => 0
        };

        if (statusCode == 0)
        {
            // Not one of ours - let the default developer-exception-page/500 handling take over.
            return false;
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = statusCode,
                Title = ReasonPhraseFor(statusCode),
                Detail = exception.Message
            }
        }).ConfigureAwait(false);
    }

    private static string ReasonPhraseFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad request",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Error"
    };
}
