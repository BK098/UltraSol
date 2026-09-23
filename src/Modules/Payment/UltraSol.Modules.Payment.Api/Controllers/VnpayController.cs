using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UltraSol.Modules.Payment.Application;

namespace UltraSol.Modules.Payment.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments/vnpay")]
public sealed class VnpayController(PaymentService service, ILogger<VnpayController> logger) : ControllerBase
{
    [HttpGet("ipn")]
    public async Task<IActionResult> Ipn(CancellationToken ct)
    {
        var values = Values();
        if (values is null)
        {
            return Reply(new("97", "Invalid parameters"));
        }
        try
        {
            return Reply(await service.IpnAsync(values, ct));
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("VNPAY IPN could not commit ({ErrorType}).", error.GetType().Name);
            return Reply(new("99", "Please retry"));
        }
    }

    [HttpGet("return")]
    public IActionResult Return()
    {
        var values = Values();
        return Ok(values is null ? new { verified = false, result = "Unverified" } : service.Return(values));
    }

    private IActionResult Reply(VnpayAcknowledgement result) => new JsonResult(new Dictionary<string, string>
    {
        ["RspCode"] = result.RspCode, ["Message"] = result.Message
    });

    private Dictionary<string, string>? Values()
    {
        if (Request.Query.Count > 40 || Request.Query.Any(pair => pair.Value.Count != 1 || pair.Key.Length > 100 || pair.Value.ToString().Length > 2048))
        {
            return null;
        }
        return Request.Query.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
    }
}
