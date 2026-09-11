namespace UltraSol.Shared.Application.Responses
{
    public static class ApiResultBuilder
    {
        public static ApiResult<T> Success<T>(T data, string? message = null, int statusCode = 200)
        {
            return new ApiResult<T>(
                isSuccess: true,
                message: message,
                data: data,
                statusCode: statusCode);
        }

        public static ApiResult<T> Error<T>(string message, int statusCode = 400, IReadOnlyDictionary<string, string[]>? errors = null, string? traceId = null)
        {
            return new ApiResult<T>(
                isSuccess: false,
                message: message,
                data: default,
                statusCode: statusCode,
                errors: errors,
                traceId: traceId);
        }
        public static ApiResult<T> Existed<T>(string? message = null, string? traceId = null)
        {
            //if (!string.IsNullOrEmpty(message))
            //{
            //    message += " Was/Were Existed";
            //}
            //else
            //{
            //    message = "Existed";
            //}
            return new ApiResult<T>(
                isSuccess: false,
                message: message,
                data: default,
                statusCode: 400,
                traceId: traceId);
        }

        public static ApiResult<T> Validation<T>(IReadOnlyDictionary<string, string[]> errors, string message = "Validation failed", string? traceId = null)
        {
            return Error<T>(
                message: message,
                statusCode: 400,
                errors: errors,
                traceId: traceId);
        }

        public static ApiResult<T> Unauthorized<T>(string message = "Unauthorized", string? traceId = null)
        {
            return Error<T>(
                message,
                statusCode: 401,
                traceId: traceId);
        }

        public static ApiResult<T> Forbidden<T>(string message = "Forbidden", string? traceId = null)
        {
            return Error<T>(
                message,
                statusCode: 403,
                traceId: traceId);
        }

        public static ApiResult<T> NotFound<T>(string message = "Resource was not found", string? traceId = null)
        {
            return Error<T>(
                message,
                statusCode: 404,
                traceId: traceId);
        }

        public static ApiResult<T> Conflict<T>(string message, string? traceId = null)
        {
            return Error<T>(
                message,
                statusCode: 409,
                traceId: traceId);
        }

        public static ApiResult<T> Unexpected<T>(string? traceId = null)
        {
            return Error<T>(
                message: "An unexpected error occurred.",
                statusCode: 500,
                traceId: traceId);
        }
    }
}