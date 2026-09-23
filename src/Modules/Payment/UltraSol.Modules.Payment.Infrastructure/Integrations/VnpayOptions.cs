namespace UltraSol.Modules.Payment.Infrastructure.Integrations;

public sealed class VnpayOptions
{
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string GatewayUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string QueryUrl { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    public string RefundUrl { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    public string ReturnUrl { get; set; } = "";
    public string ServerIpAddress { get; set; } = "127.0.0.1";
}
