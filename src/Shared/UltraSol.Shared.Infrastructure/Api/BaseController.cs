using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Shared.Infrastructure.Api
{
    [ApiController]
    public abstract class BaseController(ISender sender) : ControllerBase
    {
        protected ISender Sender { get; } = sender;
        protected IActionResult FromResult<T>(ApiResult<T> result)
        {
            return new ObjectResult(result)
            {
                StatusCode = result.StatusCode
            };
        }
        protected async Task<IActionResult> SendAsync<T>(IRequest<ApiResult<T>> request, CancellationToken cancellationToken)
        {
            var result = await Sender.Send(request, cancellationToken);
            return FromResult(result);
        }
    }
}