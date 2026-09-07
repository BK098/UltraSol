namespace UltraSol.Shared.Application.Responses
{
    public class ApiResult<T>
    {
        public ApiResult(bool isSuccess, string? message, T? data, int statusCode, IReadOnlyDictionary<string, string[]>? errors = null, string? traceId = null)
        {
            IsSuccess = isSuccess;
            Message = message;
            Data = data;
            StatusCode = statusCode;
            Errors = errors;
            TraceId = traceId;
            Timestamp = DateTimeOffset.UtcNow;
        }

        public bool IsSuccess { get; }
        public string? Message { get; }
        public T? Data { get; }
        public int StatusCode { get; }
        public IReadOnlyDictionary<string, string[]>? Errors { get; }
        public DateTimeOffset Timestamp { get; }
        public string? TraceId { get; }
    }
}