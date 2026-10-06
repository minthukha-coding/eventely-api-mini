namespace Eventely.Api.Common;

public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static ApiException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);
    public static ApiException Unauthorized(string message = "Authentication failed.") => new(StatusCodes.Status401Unauthorized, message);
    public static ApiException Forbidden(string message = "You are not allowed to perform this action.") => new(StatusCodes.Status403Forbidden, message);
    public static ApiException NotFound(string message = "The requested resource was not found.") => new(StatusCodes.Status404NotFound, message);
    public static ApiException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
    public static ApiException ServiceUnavailable(string message) => new(StatusCodes.Status503ServiceUnavailable, message);
    public static ApiException BadGateway(string message) => new(StatusCodes.Status502BadGateway, message);
}
