using FluentValidation;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Ordering.Application.Checkout;

public static class OrderingResults
{
    public static async Task<ApiResult<T>> Run<T>(Func<Task<ApiResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (OrderingFailure error)
        {
            return ApiResultBuilder.Error<T>(error.Message, error.Status, new Dictionary<string, string[]> { ["Code"] = [error.Code] });
        }
        catch (DomainException error)
        {
            return ApiResultBuilder.Error<T>(error.Message, 409, new Dictionary<string, string[]> { ["Code"] = [error.Code ?? "InvalidOrder"] });
        }
        catch (OverflowException)
        {
            return ApiResultBuilder.Error<T>("Quantity or amount is too large.", 400);
        }
    }
}