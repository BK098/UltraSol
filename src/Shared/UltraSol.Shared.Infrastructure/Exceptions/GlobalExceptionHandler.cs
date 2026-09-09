using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Shared.Infrastructure.Exceptions;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        ApiResult<object> response;

        switch (exception)
        {
            case ValidationException validationException:
                {
                    logger.LogWarning("Validation failed for request {TraceId}", traceId);
                    var errors = validationException.Errors
                        .GroupBy(error => error.PropertyName.StartsWith("Request.")
                            ? error.PropertyName["".Length..]
                            : error.PropertyName)
                        .ToDictionary(
                            group => group.Key,
                            group => group
                                .Select(error => error.ErrorMessage)
                                .Distinct()
                                .ToArray());

                    response = ApiResultBuilder.Validation<object>(errors, traceId: traceId);
                    break;
                }

            case UnauthorizedAccessException:
                {
                    logger.LogWarning("Unauthorized request {TraceId}", traceId);
                    response = ApiResultBuilder.Unauthorized<object>("Invalid credentials.", traceId);
                    break;
                }
            case KeyNotFoundException:
                {
                    logger.LogWarning("Resource not found for request {TraceId}", traceId);
                    response = ApiResultBuilder.NotFound<object>(traceId: traceId);
                break;
                }
            case DomainException domainException:
                {
                    logger.LogWarning(domainException, "Domain rule rejected request {TraceId}", traceId);
                    response = ApiResultBuilder.Conflict<object>(domainException.Message, traceId);
                    break;
                }

            default:
                {
                    logger.LogError(exception, "Unhandled exception for request {TraceId}", traceId);
                    response = ApiResultBuilder.Unexpected<object>(traceId);
                    break;
                }
        }

        httpContext.Response.StatusCode = response.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
