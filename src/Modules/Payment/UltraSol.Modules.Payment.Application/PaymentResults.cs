using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Payment.Application;

public sealed class PaymentFailure(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public static class PaymentResults
{
    public static async Task<ApiResult<T>> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return ApiResultBuilder.Success(await action());
        }
        catch (PaymentFailure error)
        {
            return ApiResultBuilder.Error<T>(error.Message, error.Status, new Dictionary<string, string[]> { ["Code"] = [error.Code] });
        }
        catch (DomainException error)
        {
            return ApiResultBuilder.Error<T>(error.Message, 409, new Dictionary<string, string[]> { ["Code"] = [error.Code ?? "InvalidPayment"] });
        }
        catch (OverflowException)
        {
            return ApiResultBuilder.Error<T>("Amount is outside the supported range.", 400);
        }
    }
}
