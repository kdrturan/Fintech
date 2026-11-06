using Banking.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Web.Errors;

/// <summary>
/// Turns an <see cref="Error"/> into an RFC 9457 problem response. The status code comes from the error's
/// <see cref="ErrorType"/>; clients decide what to do from <c>errorCode</c>, never from the human-readable text.
/// See docs/adr/0013.
/// </summary>
public static class ErrorResponses
{
    public const string ErrorCodeKey = "errorCode";

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown error type."),
    };

    /// <summary>Usage in a controller: <c>return this.Problem(result.Error!);</c></summary>
    public static ObjectResult Problem(this ControllerBase controller, Error error, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(controller);
        return ToProblemResult(error, controller.HttpContext, extensions);
    }

    public static ObjectResult ToProblemResult(this Error error, HttpContext httpContext, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(httpContext);

        var statusCode = StatusCodeFor(error.Type);

        // The factory adds what every problem response carries, such as the traceId.
        var problem = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>().CreateProblemDetails(
            httpContext,
            statusCode: statusCode,
            title: TitleFor(error.Type),
            detail: error.Message);
        problem.Extensions[ErrorCodeKey] = error.Code;
        foreach (var (key, value) in extensions ?? new Dictionary<string, object?>())
        {
            problem.Extensions[key] = value;
        }

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "The request is not valid.",
        ErrorType.NotFound => "The resource was not found.",
        ErrorType.Conflict => "The request conflicts with the current state.",
        _ => "The request cannot be completed.",
    };
}
